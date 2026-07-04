namespace TransportPlatform.Application.Abstractions;

/// <summary>
/// Port to an EXTERNAL payment provider. The platform never sees or stores card data:
/// the customer pays on the provider's hosted checkout and the provider calls us back via
/// a signed webhook. Implementations are pluggable (Sandbox for dev/tests; PayPal in prod)
/// so swapping providers is a config change, not a rewrite.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Provider identifier persisted on the payment record (e.g. "Sandbox", "PayPal").</summary>
    string Name { get; }

    /// <summary>Create a hosted checkout session and return where to send the customer.</summary>
    Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify a webhook's authenticity and parse it. Returns null if verification fails — the
    /// caller must then reject the request. Async because real providers (PayPal) verify the
    /// signature via an API call, and may capture the order as part of handling the event.
    /// All inbound headers are passed so each provider can read whatever it signs over.
    /// </summary>
    Task<PaymentWebhook?> VerifyAndParseWebhookAsync(
        string payload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refund a previously-captured payment. <paramref name="request"/> carries the gateway's
    /// transaction reference (capture id) and an idempotency key so a retry never double-refunds.
    /// </summary>
    Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default);
}

public sealed record CheckoutRequest(
    Guid BookingId,
    string BookingReference,
    decimal Amount,
    string Currency,
    string CustomerEmail,
    string IdempotencyKey);

public sealed record CheckoutSession(string CheckoutUrl, string GatewayReference);

/// <summary>
/// A parsed, signature-verified gateway event. <paramref name="Amount"/>/<paramref name="Currency"/>
/// carry the amount the gateway actually captured (when the provider reports it) so the caller can
/// re-verify it equals the booking total before confirming — defence against a signature-valid
/// webhook for the wrong amount. Null when the provider doesn't report an amount for the event.
/// </summary>
public sealed record PaymentWebhook(
    string GatewayReference, string BookingReference, bool Succeeded, decimal? Amount = null, string? Currency = null);

public sealed record RefundRequest(string GatewayTransactionRef, decimal Amount, string Currency, string IdempotencyKey);

public sealed record RefundResult(bool Succeeded, string? GatewayRefundRef, string? Error);
