namespace TransportPlatform.Application.Abstractions;

/// <summary>
/// Port to an EXTERNAL payment provider. The platform never sees or stores card data:
/// the customer pays on the provider's hosted checkout and the provider calls us back via
/// a signed webhook. Implementations are pluggable (Sandbox now; a regional gateway later)
/// so swapping providers is a config change, not a rewrite.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Provider identifier persisted on the payment record (e.g. "Sandbox").</summary>
    string Name { get; }

    /// <summary>Create a hosted checkout session and return where to send the customer.</summary>
    Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify a webhook's authenticity (signature/HMAC) and parse it. Returns null if the
    /// signature is invalid — the caller must then reject the request.
    /// </summary>
    PaymentWebhook? VerifyAndParseWebhook(string payload, string? signatureHeader);
}

public sealed record CheckoutRequest(
    Guid BookingId,
    string BookingReference,
    decimal Amount,
    string Currency,
    string CustomerEmail,
    string IdempotencyKey);

public sealed record CheckoutSession(string CheckoutUrl, string GatewayReference);

public sealed record PaymentWebhook(string GatewayReference, string BookingReference, bool Succeeded);
