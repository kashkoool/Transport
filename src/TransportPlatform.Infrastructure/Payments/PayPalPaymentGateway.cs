using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Infrastructure.Payments;

/// <summary>
/// Real <see cref="IPaymentGateway"/> backed by the PayPal REST v2 API over plain HttpClient
/// (no SDK — smaller supply-chain surface). Card data never touches us: the buyer approves on
/// PayPal's hosted page. We confirm via a signature-verified webhook, capturing the order on
/// CHECKOUT.ORDER.APPROVED. Configure the PayPal webhook to send only the events handled here.
/// </summary>
public sealed class PayPalPaymentGateway(IHttpClientFactory httpClientFactory, IOptions<PaymentOptions> options)
    : IPaymentGateway, IDisposable
{
    public const string HttpClientName = "paypal";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly PaymentOptions _options = options.Value;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _token;
    private DateTimeOffset _tokenExpiresAt;

    public string Name => "PayPal";

    public async Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var http = httpClientFactory.CreateClient(HttpClientName);
        var body = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    reference_id = request.BookingReference,
                    custom_id = request.BookingReference,
                    amount = new { currency_code = request.Currency, value = Money(request.Amount) },
                },
            },
            application_context = new
            {
                return_url = _options.ReturnUrl,
                cancel_url = _options.CancelUrl,
                user_action = "PAY_NOW",
            },
        };

        using var req = await AuthorizedAsync(HttpMethod.Post, "/v2/checkout/orders", http, cancellationToken);
        req.Headers.TryAddWithoutValidation("PayPal-Request-Id", request.IdempotencyKey); // idempotent create
        req.Content = JsonContent.Create(body);
        using var resp = await http.SendAsync(req, cancellationToken);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cancellationToken));
        var id = doc.RootElement.GetProperty("id").GetString() ?? "";
        var approveUrl = doc.RootElement.GetProperty("links").EnumerateArray()
            .FirstOrDefault(l => l.GetProperty("rel").GetString() is "approve" or "payer-action")
            .GetProperty("href").GetString() ?? "";
        return new CheckoutSession(approveUrl, id);
    }

    public async Task<PaymentWebhook?> VerifyAndParseWebhookAsync(
        string payload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (!await VerifySignatureAsync(payload, headers, cancellationToken))
            return null; // bad/forged signature → reject

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var eventType = root.TryGetProperty("event_type", out var et) ? et.GetString() : null;
        if (!root.TryGetProperty("resource", out var resource))
            return null;

        switch (eventType)
        {
            case "CHECKOUT.ORDER.APPROVED":
                // Buyer approved — capture the order now (money only moves on capture).
                return await CaptureOrderAsync(resource, cancellationToken);

            case "PAYMENT.CAPTURE.COMPLETED":
                return new PaymentWebhook(
                    GetString(resource, "id"), GetString(resource, "custom_id"), Succeeded: true);

            case "PAYMENT.CAPTURE.DENIED":
            case "CHECKOUT.ORDER.DECLINED":
                return new PaymentWebhook(
                    GetString(resource, "id"), GetString(resource, "custom_id"), Succeeded: false);

            default:
                return null; // unhandled (configure the webhook to send only the events above)
        }
    }

    public async Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default)
    {
        var http = httpClientFactory.CreateClient(HttpClientName);
        using var req = await AuthorizedAsync(
            HttpMethod.Post, $"/v2/payments/captures/{request.GatewayTransactionRef}/refund", http, cancellationToken);
        req.Headers.TryAddWithoutValidation("PayPal-Request-Id", request.IdempotencyKey); // idempotent refund
        req.Content = JsonContent.Create(new
        {
            amount = new { value = Money(request.Amount), currency_code = request.Currency },
        });
        using var resp = await http.SendAsync(req, cancellationToken);
        if (!resp.IsSuccessStatusCode)
            return new RefundResult(false, null, $"paypal_refund_http_{(int)resp.StatusCode}");

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cancellationToken));
        return new RefundResult(true, doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null, null);
    }

    private async Task<PaymentWebhook?> CaptureOrderAsync(JsonElement orderResource, CancellationToken ct)
    {
        var orderId = GetString(orderResource, "id");
        var bookingRef = orderResource.TryGetProperty("purchase_units", out var units)
            && units.ValueKind == JsonValueKind.Array && units.GetArrayLength() > 0
                ? GetString(units[0], "reference_id")
                : "";
        if (string.IsNullOrEmpty(orderId))
            return null;

        var http = httpClientFactory.CreateClient(HttpClientName);
        using var req = await AuthorizedAsync(HttpMethod.Post, $"/v2/checkout/orders/{orderId}/capture", http, ct);
        req.Content = JsonContent.Create(new { });
        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            return new PaymentWebhook(orderId, bookingRef, Succeeded: false);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var completed = doc.RootElement.TryGetProperty("status", out var st) && st.GetString() == "COMPLETED";
        // The capture id is the gateway reference we later refund against.
        var captureId = doc.RootElement.TryGetProperty("purchase_units", out var pu)
            && pu.GetArrayLength() > 0
            && pu[0].TryGetProperty("payments", out var pay)
            && pay.TryGetProperty("captures", out var caps)
            && caps.GetArrayLength() > 0
                ? GetString(caps[0], "id")
                : orderId;
        return new PaymentWebhook(captureId, bookingRef, completed);
    }

    private async Task<bool> VerifySignatureAsync(
        string payload, IReadOnlyDictionary<string, string> headers, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookId))
            return false;

        var verifyBody = new JsonObject
        {
            ["auth_algo"] = Header(headers, "PAYPAL-AUTH-ALGO"),
            ["cert_url"] = Header(headers, "PAYPAL-CERT-URL"),
            ["transmission_id"] = Header(headers, "PAYPAL-TRANSMISSION-ID"),
            ["transmission_sig"] = Header(headers, "PAYPAL-TRANSMISSION-SIG"),
            ["transmission_time"] = Header(headers, "PAYPAL-TRANSMISSION-TIME"),
            ["webhook_id"] = _options.WebhookId,
            ["webhook_event"] = JsonNode.Parse(payload),
        };

        var http = httpClientFactory.CreateClient(HttpClientName);
        using var req = await AuthorizedAsync(HttpMethod.Post, "/v1/notifications/verify-webhook-signature", http, ct);
        req.Content = new StringContent(verifyBody.ToJsonString(), Encoding.UTF8, "application/json");
        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            return false;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.TryGetProperty("verification_status", out var status)
            && status.GetString() == "SUCCESS";
    }

    private async Task<HttpRequestMessage> AuthorizedAsync(HttpMethod method, string path, HttpClient http, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(http, ct);
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    private async Task<string> GetAccessTokenAsync(HttpClient http, CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            return _token;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
                return _token;

            using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            req.Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("grant_type", "client_credentials")]);
            using var resp = await http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var token = await resp.Content.ReadFromJsonAsync<TokenResponse>(Web, ct)
                ?? throw new InvalidOperationException("PayPal returned no access token.");
            _token = token.AccessToken;
            // Refresh a minute early; never trust an absurdly small expiry.
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn) - 60);
            return _token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static string Money(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.GetString() ?? "" : "";

    private static string? Header(IReadOnlyDictionary<string, string> headers, string name) =>
        headers.TryGetValue(name, out var value) ? value : null;

    public void Dispose() => _tokenLock.Dispose();

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn);
}
