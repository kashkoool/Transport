using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.Application.Bookings;

/// <summary>
/// Shared helper that processes a recorded (Pending) refund through the gateway AFTER the
/// cancellation transaction has committed — never inside a DB transaction. Best-effort: a gateway
/// failure leaves the refund Pending for the <c>RefundReconciliationWorker</c> to retry (the cancel
/// already stands).
/// </summary>
internal static class RefundProcessing
{
    /// <summary>
    /// Record a full-amount Pending refund for a captured payment inside the caller's transaction,
    /// returning its id to process after commit. The idempotency key MUST be <c>refund-{bookingId:N}</c>
    /// on every path (customer cancel, trip cancel, webhook compensation) so the unique index makes a
    /// booking refundable at most once regardless of which path fires.
    /// </summary>
    public static Guid RecordRefund(
        IApplicationDbContext db, Payment payment, Guid bookingId, string reason, Money? amount = null)
    {
        var refund = new Refund(
            payment.Id, bookingId, amount ?? new Money(payment.Amount, payment.Currency), reason, $"refund-{bookingId:N}");
        db.Refunds.Add(refund);
        return refund.Id;
    }

    public static async Task<bool> TryProcessAsync(
        IApplicationDbContext db, IPaymentGateway gateway, Guid refundId, CancellationToken ct)
    {
        var refund = await db.Refunds.FirstOrDefaultAsync(r => r.Id == refundId, ct);
        if (refund is null)
            return false;
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == refund.PaymentId, ct);
        if (payment?.GatewayTxnRef is null)
            return false;

        try
        {
            var result = await gateway.RefundAsync(
                new RefundRequest(payment.GatewayTxnRef, refund.Amount, refund.Currency, refund.IdempotencyKey), ct);
            if (result.Succeeded)
            {
                refund.MarkCompleted(result.GatewayRefundRef ?? string.Empty);
                payment.MarkRefunded();
                await db.SaveChangesAsync(ct);
                return true;
            }

            // Non-success (e.g. a transient gateway error) → leave the refund Pending so a
            // reconciliation job can retry; the gateway call is idempotent on the refund's key.
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Same: leave Pending for a safe retry.
            return false;
        }
    }
}
