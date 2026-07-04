using TransportPlatform.Application.Payments;

namespace TransportPlatform.Api.Workers;

/// <summary>
/// Periodically retries refunds left <c>Pending</c> by a transient gateway failure, so a customer's
/// money is never permanently stranded because one refund call timed out. The gateway refund is
/// idempotent on the refund's key, so this is safe to run repeatedly and on multiple instances.
/// </summary>
public sealed class RefundReconciliationWorker(IServiceProvider services, ILogger<RefundReconciliationWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);
    private const int BatchSize = 50;

    private static readonly Action<ILogger, int, Exception?> LogRetried =
        LoggerMessage.Define<int>(
            LogLevel.Information, new EventId(1, nameof(LogRetried)), "Reconciled {Count} pending refund(s).");

    private static readonly Action<ILogger, Exception?> LogFailed =
        LoggerMessage.Define(
            LogLevel.Error, new EventId(2, nameof(LogFailed)), "Refund reconciliation tick failed; will retry.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = services.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<ProcessPendingRefundsHandler>();
                var processed = await handler.HandleAsync(BatchSize, stoppingToken);
                if (processed > 0)
                    LogRetried(logger, processed, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
