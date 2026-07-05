using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.Api.Workers;

/// <summary>
/// Prunes rows that grow unbounded once they've served their purpose: processed outbox messages
/// (kept only as a delivery guarantee) and dead refresh tokens (revoked or expired). Runs daily and
/// deletes anything older than the retention window (config <c>DataRetention:Days</c>, default 14),
/// keeping both tables — and their indexes — from bloating over the life of the deployment.
/// </summary>
public sealed class DataRetentionWorker(IServiceProvider services, IConfiguration config, ILogger<DataRetentionWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private static readonly Action<ILogger, int, int, Exception?> LogPruned =
        LoggerMessage.Define<int, int>(
            LogLevel.Information, new EventId(1, nameof(LogPruned)),
            "Data retention pruned {Outbox} outbox message(s) and {Tokens} refresh token(s).");

    private static readonly Action<ILogger, Exception?> LogFailed =
        LoggerMessage.Define(
            LogLevel.Error, new EventId(2, nameof(LogFailed)), "Data retention tick failed; will retry.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retentionDays = config.GetValue<int?>("DataRetention:Days") ?? 14;

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();

                var cutoff = clock.UtcNow.AddDays(-retentionDays);

                // Processed outbox rows exist only as a crash-recovery guarantee; once processed and
                // past the window they're dead weight.
                var outboxDeleted = await db.OutboxMessages
                    .Where(m => m.ProcessedAtUtc != null && m.ProcessedAtUtc < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                // Refresh tokens that are revoked or expired can never be used again; drop them once
                // they've aged past the window (measured on whichever timestamp made them dead).
                var tokensDeleted = await db.RefreshTokens
                    .Where(t => (t.RevokedAtUtc != null && t.RevokedAtUtc < cutoff)
                                || (t.RevokedAtUtc == null && t.ExpiresAtUtc < cutoff))
                    .ExecuteDeleteAsync(stoppingToken);

                if (outboxDeleted > 0 || tokensDeleted > 0)
                    LogPruned(logger, outboxDeleted, tokensDeleted, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
