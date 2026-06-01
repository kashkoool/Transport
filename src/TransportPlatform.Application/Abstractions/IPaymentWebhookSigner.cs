namespace TransportPlatform.Application.Abstractions;

/// <summary>
/// A development/test seam for signing a webhook payload exactly as the gateway would, so a
/// SIMULATED gateway callback passes the real signature check in <see cref="IPaymentGateway"/>.
/// It exists only to drive the local "pay → confirm → ticket" flow without a real provider;
/// production payment flows never use it. Kept off <see cref="IPaymentGateway"/> on purpose —
/// signing our own outgoing payload is not part of the real gateway contract.
/// </summary>
public interface IPaymentWebhookSigner
{
    string Sign(string payload);
}
