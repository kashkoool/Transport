using FluentValidation;
using TransportPlatform.Api.Security;
using TransportPlatform.Application.Bookings;

namespace TransportPlatform.Api.Endpoints;

public static class BookingEndpoints
{
    // The customer is taken from the JWT, never the request body — so requests carry only
    // the trip + seats/passengers.
    public sealed record HoldRequest(Guid TripId, IReadOnlyList<int> SeatNumbers);
    public sealed record CreateBookingRequest(Guid TripId, IReadOnlyList<PassengerInput> Passengers);

    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        // Login-required: a booking belongs to the authenticated customer. Abuse-sensitive
        // writes get the stricter rate-limit tier.
        var group = app.MapGroup("/api/bookings").WithTags("Bookings")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Sensitive);

        group.MapPost("/hold", async (
            HoldRequest body, HoldSeatsHandler handler,
            IValidator<HoldSeatsCommand> validator, CancellationToken ct) =>
        {
            var command = new HoldSeatsCommand(body.TripId, body.SeatNumbers);
            await validator.ValidateAndThrowAsync(command, ct);
            var result = await handler.HandleAsync(command, ct);
            return Results.Ok(result);
        })
        .WithName("HoldSeats")
        .WithSummary("Hold seats for ~10 minutes while the customer pays.");

        group.MapPost("/", async (
            CreateBookingRequest body, HttpRequest request,
            CreateBookingHandler handler, IValidator<CreateBookingCommand> validator, CancellationToken ct) =>
        {
            // Idempotency-Key header makes booking creation safe to retry.
            var idempotencyKey = request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return Results.BadRequest(new { code = "idempotency.required", message = "Idempotency-Key header is required." });

            var command = new CreateBookingCommand(body.TripId, body.Passengers, idempotencyKey);
            await validator.ValidateAndThrowAsync(command, ct);
            var result = await handler.HandleAsync(command, ct);
            return Results.Ok(result);
        })
        .WithName("CreateBooking")
        .WithSummary("Create a pending booking from held seats (idempotent).");

        group.MapGet("/{id:guid}/ticket", async (
            Guid id, GetTicketHandler handler, CancellationToken ct) =>
        {
            var ticket = await handler.HandleAsync(new GetTicketQuery(id), ct);
            return Results.Ok(ticket);
        })
        .WithName("GetTicket")
        .WithSummary("Get the boarding ticket (with QR payload) for your booking.");

        return app;
    }
}
