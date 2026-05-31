using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
public sealed partial class SandboxPaymentGateway(IOptions<PaymentOptions> options) : IPaymentGateway
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    // A valid signature is exactly a hex-encoded SHA-256 (64 chars). The format gate below
    // rejects anything else in O(1) BEFORE any allocation/hashing, so a flood of oversized
    // bodies can't burn CPU/memory (DoS-adjacent resource exhaustion).
    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.IgnoreCase)]
    private static partial Regex SignatureFormat();

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
        // O(1) upfront format gate: reject wrong-length / non-hex / null before any work.
        if (string.IsNullOrEmpty(signatureHeader) || !SignatureFormat().IsMatch(signatureHeader))
            return null;
        if (!VerifySignature(payload, signatureHeader))
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
        // Constant-time comparison on decoded bytes (both 32 bytes for SHA-256) to avoid
        // signature-comparison timing leaks.
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expected), Convert.FromHexString(signatureHeader));
    }

    private sealed record WebhookDto(string? GatewayReference, string BookingReference, bool Succeeded);
}
