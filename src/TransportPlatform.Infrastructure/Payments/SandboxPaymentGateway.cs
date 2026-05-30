using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Infrastructure.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";
    public string Provider { get; set; } = "Sandbox";
    public string WebhookSecret { get; set; } = "dev-secret";
    public string CheckoutBaseUrl { get; set; } = "https://sandbox.local/checkout";
}

/// <summary>
/// A stand-in for a real external gateway. It mints a fake hosted-checkout URL and verifies
/// webhooks with an HMAC signature — exercising the SAME contract a real regional provider
/// will use, so swapping it later is just another <see cref="IPaymentGateway"/> implementation.
/// Crucially, it never receives or stores card data.
/// </summary>
public sealed class SandboxPaymentGateway(IOptions<PaymentOptions> options) : IPaymentGateway
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PaymentOptions _options = options.Value;

    public string Name => "Sandbox";

    public Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var gatewayRef = $"SBX-{Guid.NewGuid():N}";
        var url = $"{_options.CheckoutBaseUrl}?ref={gatewayRef}&booking={Uri.EscapeDataString(request.BookingReference)}";
        return Task.FromResult(new CheckoutSession(url, gatewayRef));
    }

    public PaymentWebhook? VerifyAndParseWebhook(string payload, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || !VerifySignature(payload, signatureHeader))
            return null;

        var dto = JsonSerializer.Deserialize<WebhookDto>(payload, WebJsonOptions);
        if (dto is null || string.IsNullOrWhiteSpace(dto.BookingReference))
            return null;

        return new PaymentWebhook(dto.GatewayReference ?? "", dto.BookingReference, dto.Succeeded);
    }

    /// <summary>HMAC-SHA256 of the body, hex-encoded — the shape most gateways use.</summary>
    public string ComputeSignature(string payload)
    {
        var key = Encoding.UTF8.GetBytes(_options.WebhookSecret);
        var hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hash);
    }

    private bool VerifySignature(string payload, string signatureHeader)
    {
        var expected = ComputeSignature(payload);
        // Constant-time comparison to avoid timing attacks.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signatureHeader));
    }

    private sealed record WebhookDto(string? GatewayReference, string BookingReference, bool Succeeded);
}
