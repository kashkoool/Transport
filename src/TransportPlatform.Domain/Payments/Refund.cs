using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Payments;

/// <summary>
/// A refund issued against a completed <see cref="Payment"/> through the external gateway. Like
/// payments, it stores only references — never card data. Idempotency key guards against
/// double-refunding on a retry.
/// </summary>
public sealed class Refund : AggregateRoot
{
    public Guid PaymentId { get; private set; }
    public Guid BookingId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "SYP";
    public RefundStatus Status { get; private set; } = RefundStatus.Pending;
    public string? GatewayRefundRef { get; private set; }
    public string Reason { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = null!;

    private Refund() { } // EF

    public Refund(Guid paymentId, Guid bookingId, Money amount, string reason, string idempotencyKey)
    {
        if (paymentId == Guid.Empty)
            throw new DomainException("refund.payment_required", "A refund must reference a payment.");
        if (bookingId == Guid.Empty)
            throw new DomainException("refund.booking_required", "A refund must reference a booking.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("refund.idempotency_required", "An idempotency key is required.");

        PaymentId = paymentId;
        BookingId = bookingId;
        Amount = amount.Amount;
        Currency = amount.Currency;
        Reason = string.IsNullOrWhiteSpace(reason) ? "Refund" : reason.Trim();
        IdempotencyKey = idempotencyKey;
    }

    public void MarkCompleted(string gatewayRefundRef)
    {
        if (Status == RefundStatus.Completed)
            return;
        GatewayRefundRef = gatewayRefundRef;
        Status = RefundStatus.Completed;
    }

    /// <summary>
    /// Mark a refund permanently failed (gateway declined). A Pending refund is retried by the
    /// reconciliation worker; Failed means "give up and alert" — so a Completed refund can never
    /// regress to Failed.
    /// </summary>
    public void MarkFailed()
    {
        if (Status == RefundStatus.Failed)
            return; // idempotent
        if (Status == RefundStatus.Completed)
            throw new DomainException("refund.invalid_transition", "A completed refund cannot be marked failed.");
        Status = RefundStatus.Failed;
    }
}
