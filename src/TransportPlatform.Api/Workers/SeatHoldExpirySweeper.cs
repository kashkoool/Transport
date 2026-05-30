using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.Api.Workers;

/// <summary>
/// Periodically deletes expired, unconsumed seat holds so the seats they were blocking
/// become bookable again. Uses a short interval; safe to run on multiple instances because
/// the delete is set-based and idempotent.
/// </summary>
public sealed class SeatHoldExpirySweeper(IServiceProvider services, ILogger<SeatHoldExpirySweeper> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    // Cached, allocation-free logging delegates (satisfies CA1848).
    private static readonly Action<ILogger, int, Exception?> LogSwept =
        LoggerMessage.Define<int>(
            LogLevel.Information, new EventId(1, nameof(LogSwept)), "Swept {Count} expired seat holds.");

    private static readonly Action<ILogger, Exception?> LogSweepFailed =
        LoggerMessage.Define(
            LogLevel.Error, new EventId(2, nameof(LogSweepFailed)), "Seat-hold sweep failed; will retry next tick.");

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
                var now = clock.UtcNow;

                var removed = await db.SeatHolds
                    .Where(h => !h.Consumed && h.ExpiresAtUtc <= now)
                    .ExecuteDeleteAsync(stoppingToken);

                if (removed > 0)
                    LogSwept(logger, removed, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogSweepFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
