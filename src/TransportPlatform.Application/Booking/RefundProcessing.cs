using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Bookings;

/// <summary>
/// Shared helper that processes a recorded (Pending) refund through the gateway AFTER the
/// cancellation transaction has committed — never inside a DB transaction. Best-effort: a gateway
/// failure leaves the refund Pending for a reconciliation job to retry (the cancel already stands).
/// </summary>
internal static class RefundProcessing
{
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
            }
            else
            {
                refund.MarkFailed();
            }
            await db.SaveChangesAsync(ct);
            return result.Succeeded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Leave the refund Pending; the gateway call is idempotent on the refund's key, so a
            // later retry is safe.
            return false;
        }
    }
}
