using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Payments;

/// <summary>
/// A payment processed by an EXTERNAL gateway. We deliberately store NO card data —
/// only the gateway's transaction reference, status and amount. Card entry happens on the
/// gateway's hosted page (see STACK_RECOMMENDATION.md / ARCHITECTURE.md).
/// </summary>
public sealed class Payment : AggregateRoot
{
    public Guid BookingId { get; private set; }
    public string Gateway { get; private set; } = null!;
    public string? GatewayTxnRef { get; private set; }
    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "SYP";

    /// <summary>Dedupe key so a retried checkout request never creates a second charge.</summary>
    public string IdempotencyKey { get; private set; } = null!;

    private Payment() { } // EF

    public Payment(Guid bookingId, string gateway, Money amount, string idempotencyKey)
    {
        if (bookingId == Guid.Empty)
            throw new DomainException("payment.booking_required", "A payment must reference a booking.");
        if (string.IsNullOrWhiteSpace(gateway))
            throw new DomainException("payment.gateway_required", "A payment gateway is required.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("payment.idempotency_required", "An idempotency key is required.");

        BookingId = bookingId;
        Gateway = gateway;
        Amount = amount.Amount;
        Currency = amount.Currency;
        IdempotencyKey = idempotencyKey;
    }

    public void AttachGatewayReference(string gatewayTxnRef) => GatewayTxnRef = gatewayTxnRef;

    public void MarkCompleted(string gatewayTxnRef)
    {
        if (Status == PaymentStatus.Completed)
            return; // idempotent for duplicate webhooks
        GatewayTxnRef = gatewayTxnRef;
        Status = PaymentStatus.Completed;
    }

    public void MarkFailed() => Status = PaymentStatus.Failed;
}
