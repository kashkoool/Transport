using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.Api.Workers;

/// <summary>
/// Drains the outbox: picks up unprocessed domain events and dispatches them to their side
/// effects (e.g. a booking-confirmation email) via <see cref="IIntegrationEventDispatcher"/>.
/// A message is marked processed only after its side effect succeeds (at-least-once delivery);
/// a failure is recorded and retried next tick, up to a cap, after which it's given up + logged
/// so a single poison message can't loop forever.
/// </summary>
public sealed class OutboxPublisher(IServiceProvider services, ILogger<OutboxPublisher> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 50;
    private const int MaxAttempts = 5;

    // Cached, allocation-free logging delegates (satisfies CA1848).
    private static readonly Action<ILogger, string, Guid, Exception?> LogMessageFailed =
        LoggerMessage.Define<string, Guid>(
            LogLevel.Warning, new EventId(1, nameof(LogMessageFailed)),
            "Outbox dispatch failed for {Type} ({Id}); will retry.");

    private static readonly Action<ILogger, string, Guid, Exception?> LogGaveUp =
        LoggerMessage.Define<string, Guid>(
            LogLevel.Error, new EventId(2, nameof(LogGaveUp)),
            "Outbox dispatch gave up on {Type} ({Id}) after max attempts.");

    private static readonly Action<ILogger, Exception?> LogBatchFailed =
        LoggerMessage.Define(
            LogLevel.Error, new EventId(3, nameof(LogBatchFailed)), "Outbox drain failed; will retry next tick.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Only ONE instance drains per tick (Postgres advisory lock), so a domain-event side
                // effect like a confirmation email is dispatched exactly once even when the API runs
                // as multiple instances behind a load balancer.
                await PgLeaderLock.TryRunAsLeaderAsync(db, PgLeaderLock.Outbox,
                    () => DrainAsync(scope.ServiceProvider, db, stoppingToken), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogBatchFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DrainAsync(IServiceProvider sp, ApplicationDbContext db, CancellationToken stoppingToken)
    {
        var clock = sp.GetRequiredService<IClock>();
        var dispatcher = sp.GetRequiredService<IIntegrationEventDispatcher>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(stoppingToken);

        foreach (var message in pending)
        {
            try
            {
                await dispatcher.DispatchAsync(message.Type, message.Payload, stoppingToken);
                message.MarkProcessed(clock.UtcNow);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.RecordFailure(ex.GetType().Name);
                if (message.Attempts >= MaxAttempts)
                {
                    LogGaveUp(logger, message.Type, message.Id, ex);
                    message.MarkProcessed(clock.UtcNow); // stop retrying a poison message
                }
                else
                {
                    LogMessageFailed(logger, message.Type, message.Id, ex);
                }
            }
        }

        if (pending.Count > 0)
            await db.SaveChangesAsync(stoppingToken);
    }
}
