using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Bookings;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.Application.Payments;

/// <summary>
/// Retries refunds that are still <see cref="RefundStatus.Pending"/> — i.e. a gateway refund call
/// failed transiently (timeout/5xx) and was left for retry. The gateway refund is idempotent on the
/// refund's key, so re-processing an already-refunded one is safe. Driven by a background worker so
/// a customer's money is never permanently stranded by one flaky gateway call.
/// </summary>
public sealed class ProcessPendingRefundsHandler(IApplicationDbContext db, IPaymentGateway gateway)
{
    public async Task<int> HandleAsync(int batchSize, CancellationToken ct)
    {
        var pending = await db.Refunds
            .Where(r => r.Status == RefundStatus.Pending)
            .OrderBy(r => r.Id)
            .Take(batchSize)
            .Select(r => r.Id)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var id in pending)
        {
            if (await RefundProcessing.TryProcessAsync(db, gateway, id, ct))
                processed++;
        }

        return processed;
    }
}
