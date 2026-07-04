using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Bookings;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.Application.Admin;

public sealed record RefundListItem(
    Guid Id, Guid BookingId, string BookingReference, decimal Amount, string Currency,
    string Status, string Reason, DateTimeOffset CreatedAtUtc);

/// <summary>
/// Admin refund oversight: list refunds (optionally filtered by status) so stuck/failed ones are
/// visible for follow-up. Newest first, bounded. Read-only, platform-wide.
/// </summary>
public sealed class ListRefundsHandler(IApplicationDbContext db)
{
    private const int MaxResults = 200;

    public async Task<IReadOnlyList<RefundListItem>> HandleAsync(string? status, CancellationToken ct)
    {
        var query = db.Refunds.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RefundStatus>(status, ignoreCase: true, out var parsed))
            query = query.Where(r => r.Status == parsed);

        return await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(MaxResults)
            .Select(r => new RefundListItem(
                r.Id,
                r.BookingId,
                db.Bookings.Where(b => b.Id == r.BookingId).Select(b => b.Reference).FirstOrDefault() ?? "",
                r.Amount,
                r.Currency,
                r.Status.ToString(),
                r.Reason,
                r.CreatedAtUtc))
            .ToListAsync(ct);
    }
}

public sealed record RetryRefundResult(bool Succeeded, string Status);

/// <summary>
/// Admin manually re-processes a single Pending refund through the gateway (e.g. after fixing a
/// gateway-side issue), instead of waiting for the reconciliation worker. Idempotent on the refund
/// key, so retrying an already-completed refund is safe.
/// </summary>
public sealed class RetryRefundHandler(IApplicationDbContext db, IPaymentGateway gateway)
{
    public async Task<RetryRefundResult> HandleAsync(Guid refundId, CancellationToken ct)
    {
        var refund = await db.Refunds.FirstOrDefaultAsync(r => r.Id == refundId, ct)
            ?? throw new NotFoundException("Refund", refundId);

        if (refund.Status == RefundStatus.Completed)
            return new RetryRefundResult(true, RefundStatus.Completed.ToString());

        var succeeded = await RefundProcessing.TryProcessAsync(db, gateway, refundId, ct);
        var current = await db.Refunds.Where(r => r.Id == refundId).Select(r => r.Status).FirstAsync(ct);
        return new RetryRefundResult(succeeded, current.ToString());
    }
}
