using TransportPlatform.Api.Security;
using TransportPlatform.Application.Payments;

namespace TransportPlatform.Api.Endpoints;

public static class PaymentEndpoints
{
    public sealed record CheckoutRequestBody(Guid BookingId);

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
            var signature = request.Headers["X-Signature"].FirstOrDefault();

            var result = await handler.HandleAsync(new ProcessWebhookCommand(payload, signature), ct);
            return result.Handled ? Results.Ok(new { status = result.Status }) : Results.NotFound();
        })
        .RequireRateLimiting(RateLimitPolicies.Webhook)
        .WithName("PaymentWebhook")
        .WithSummary("Receive a signed payment result from the gateway (authoritative).");

        return app;
    }
}
