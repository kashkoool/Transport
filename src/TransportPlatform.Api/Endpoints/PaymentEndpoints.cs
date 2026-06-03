using System.Text.Json;
using TransportPlatform.Api.Security;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Payments;

namespace TransportPlatform.Api.Endpoints;

public static class PaymentEndpoints
{
    public sealed record CheckoutRequestBody(Guid BookingId);
    public sealed record SimulatePaymentBody(string BookingReference, bool Succeeded);

    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments").WithTags("Payments");

        group.MapPost("/checkout", async (
            CheckoutRequestBody body, StartCheckoutHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new StartCheckoutCommand(body.BookingId), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization() // pay only for your own booking (scoped in the handler)
        .RequireRateLimiting(RateLimitPolicies.Sensitive)
        .WithName("StartCheckout")
        .WithSummary("Begin hosted-checkout with the external gateway (no card data stored).");

        // The gateway calls this back (anonymous — security is the HMAC signature, not a session).
        // Rate-limited generously so legitimate gateway retries pass but floods are bounded.
        group.MapPost("/webhook", async (
            HttpRequest request, ProcessPaymentWebhookHandler handler, CancellationToken ct) =>
        {
            // Read the body as raw bytes then UTF-8 decode, so the HMAC is computed over exactly
            // the bytes the gateway signed (no StreamReader BOM/encoding ambiguity).
            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, ct);
            var payload = System.Text.Encoding.UTF8.GetString(buffer.ToArray());

            // Pass all inbound headers so each gateway can read whatever it signs over
            // (Sandbox: X-Signature; PayPal: the PAYPAL-* transmission headers).
            var headers = request.Headers.ToDictionary(
                h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

            var result = await handler.HandleAsync(new ProcessWebhookCommand(payload, headers), ct);
            return result.Handled ? Results.Ok(new { status = result.Status }) : Results.NotFound();
        })
        .RequireRateLimiting(RateLimitPolicies.Webhook)
        .WithName("PaymentWebhook")
        .WithSummary("Receive a signed payment result from the gateway (authoritative).");

        // DEVELOPMENT ONLY: the Sandbox gateway's hosted page isn't real, so this stands in for
        // it — the SPA calls it to make the gateway "post" a correctly-signed webhook, letting
        // the full pay → confirm → ticket journey run end-to-end locally. It is never mapped
        // outside Development, so production has no way to forge a confirmation.
        var env = app.ServiceProvider.GetService<IHostEnvironment>();
        if (env?.IsDevelopment() == true)
        {
            group.MapPost("/dev/simulate", async (
                SimulatePaymentBody body, IPaymentWebhookSigner signer,
                ProcessPaymentWebhookHandler handler, CancellationToken ct) =>
            {
                var payload = JsonSerializer.Serialize(new
                {
                    gatewayReference = $"SBX-{Guid.NewGuid():N}",
                    bookingReference = body.BookingReference,
                    succeeded = body.Succeeded,
                });
                var signature = signer.Sign(payload);
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["X-Signature"] = signature };
                var result = await handler.HandleAsync(new ProcessWebhookCommand(payload, headers), ct);
                return Results.Ok(new { status = result.Status });
            })
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Sensitive)
            .WithName("DevSimulatePayment")
            .WithSummary("DEV ONLY: simulate the gateway posting a signed payment result.");
        }

        return app;
    }
}
