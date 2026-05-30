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
        .WithName("StartCheckout")
        .WithSummary("Begin hosted-checkout with the external gateway (no card data stored).");

        // The gateway calls this back. Body is read raw so the HMAC signature can be verified.
        group.MapPost("/webhook", async (
            HttpRequest request, ProcessPaymentWebhookHandler handler, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var payload = await reader.ReadToEndAsync(ct);
            var signature = request.Headers["X-Signature"].FirstOrDefault();

            var result = await handler.HandleAsync(new ProcessWebhookCommand(payload, signature), ct);
            return result.Handled ? Results.Ok(new { status = result.Status }) : Results.NotFound();
        })
        .WithName("PaymentWebhook")
        .WithSummary("Receive a signed payment result from the gateway (authoritative).");

        return app;
    }
}
