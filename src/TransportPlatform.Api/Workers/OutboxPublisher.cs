using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.Api.Workers;

/// <summary>
/// Drains the outbox: picks up unprocessed domain events and "publishes" them (here, logs
/// them — a real system would push to email/SMS/notifications/real-time). Marking them
/// processed after the side effect guarantees at-least-once delivery.
/// </summary>
public sealed class OutboxPublisher(IServiceProvider services, ILogger<OutboxPublisher> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 50;

    // Cached, allocation-free logging delegates (satisfies CA1848).
    private static readonly Action<ILogger, string, Exception?> LogPublishing =
        LoggerMessage.Define<string>(
            LogLevel.Information, new EventId(1, nameof(LogPublishing)), "Publishing domain event {Type}");

    private static readonly Action<ILogger, Exception?> LogPublishFailed =
        LoggerMessage.Define(
            LogLevel.Error, new EventId(2, nameof(LogPublishFailed)), "Outbox publish failed; will retry next tick.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();

                var pending = await db.OutboxMessages
                    .Where(m => m.ProcessedAtUtc == null)
                    .OrderBy(m => m.OccurredAtUtc)
                    .Take(BatchSize)
                    .ToListAsync(stoppingToken);

                foreach (var message in pending)
                {
                    // Side effect placeholder: real handlers (email/notifications) go here.
                    LogPublishing(logger, message.Type, null);
                    message.MarkProcessed(clock.UtcNow);
                }

                if (pending.Count > 0)
                    await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogPublishFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
