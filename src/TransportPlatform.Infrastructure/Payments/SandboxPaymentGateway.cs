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

    /// <summary>"Sandbox" (dev/tests) or "PayPal".</summary>
    public string Provider { get; set; } = "Sandbox";

    // ── Sandbox ─────────────────────────────────────────────────────────────────────
    public string WebhookSecret { get; set; } = "dev-secret";
    public string CheckoutBaseUrl { get; set; } = "https://sandbox.local/checkout";

    // ── PayPal (REST v2) ────────────────────────────────────────────────────────────
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>https://api-m.sandbox.paypal.com (sandbox) or https://api-m.paypal.com (live).</summary>
    public string ApiBaseUrl { get; set; } = "https://api-m.sandbox.paypal.com";
    /// <summary>The configured webhook id, used to verify inbound webhook signatures.</summary>
    public string WebhookId { get; set; } = string.Empty;
    /// <summary>Where PayPal returns the buyer after approval (a web-app page).</summary>
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
}

/// <summary>
/// A stand-in for a real external gateway. It mints a fake hosted-checkout URL and verifies
/// webhooks with an HMAC signature — exercising the SAME contract a real regional provider
/// will use, so swapping it later is just another <see cref="IPaymentGateway"/> implementation.
/// Crucially, it never receives or stores card data.
/// </summary>
public sealed partial class SandboxPaymentGateway(IOptions<PaymentOptions> options)
    : IPaymentGateway, IPaymentWebhookSigner
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

    public Task<PaymentWebhook?> VerifyAndParseWebhookAsync(
        string payload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        headers.TryGetValue("X-Signature", out var signatureHeader);

        // O(1) upfront format gate: reject wrong-length / non-hex / null before any work.
        if (string.IsNullOrEmpty(signatureHeader) || !SignatureFormat().IsMatch(signatureHeader))
            return Task.FromResult<PaymentWebhook?>(null);
        if (!VerifySignature(payload, signatureHeader))
            return Task.FromResult<PaymentWebhook?>(null);

        var dto = JsonSerializer.Deserialize<WebhookDto>(payload, WebJsonOptions);
        if (dto is null || string.IsNullOrWhiteSpace(dto.BookingReference))
            return Task.FromResult<PaymentWebhook?>(null);

        return Task.FromResult<PaymentWebhook?>(new PaymentWebhook(dto.GatewayReference ?? "", dto.BookingReference, dto.Succeeded));
    }

    /// <summary>Sandbox refund: always succeeds and mints a fake refund reference.</summary>
    public Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RefundResult(Succeeded: true, GatewayRefundRef: $"SBX-REF-{Guid.NewGuid():N}", Error: null));

    /// <summary>
    /// <see cref="IPaymentWebhookSigner"/> seam: sign a payload as the gateway would, so a
    /// simulated dev callback passes <see cref="VerifyAndParseWebhook"/>. Delegates to the same
    /// HMAC used to verify, so simulation and verification can never drift apart.
    /// </summary>
    public string Sign(string payload) => ComputeSignature(payload);

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
