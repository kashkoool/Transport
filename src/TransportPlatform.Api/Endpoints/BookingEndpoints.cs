using FluentValidation;
using TransportPlatform.Api.Security;
using TransportPlatform.Application.Bookings;
using TransportPlatform.Application.Promotions;

namespace TransportPlatform.Api.Endpoints;

public static class BookingEndpoints
{
    // The customer is taken from the JWT, never the request body — so requests carry only
    // the trip + seats/passengers.
    public sealed record HoldRequest(Guid TripId, IReadOnlyList<int> SeatNumbers);
    public sealed record CreateBookingRequest(Guid TripId, IReadOnlyList<PassengerInput> Passengers, string? PromoCode, string? ContactPhone = null);
    public sealed record ChangeSeatBody(int FromSeat, int ToSeat);

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

            var command = new CreateBookingCommand(body.TripId, body.Passengers, idempotencyKey, body.PromoCode, body.ContactPhone);
            await validator.ValidateAndThrowAsync(command, ct);
            var result = await handler.HandleAsync(command, ct);
            return Results.Ok(result);
        })
        .WithName("CreateBooking")
        .WithSummary("Create a pending booking from held seats (idempotent).");

        group.MapGet("/", async (ListMyBookingsHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListMyBookingsQuery(), ct)))
        .WithName("ListMyBookings")
        .WithSummary("List the authenticated customer's bookings (newest first).");

        group.MapGet("/{id:guid}/ticket", async (
            Guid id, GetTicketHandler handler, CancellationToken ct) =>
        {
            var ticket = await handler.HandleAsync(new GetTicketQuery(id), ct);
            return Results.Ok(ticket);
        })
        .WithName("GetTicket")
        .WithSummary("Get the boarding ticket (with QR payload) for your booking.");

        group.MapPost("/{id:guid}/cancel", async (
            Guid id, CancelBookingHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new CancelBookingCommand(id), ct)))
        .WithName("CancelBooking")
        .WithSummary("Cancel your booking before departure; refund is tiered by time-to-departure (100/50/0%).");

        group.MapPost("/{id:guid}/change-seat", async (
            Guid id, ChangeSeatBody body, ChangeSeatHandler handler,
            IValidator<ChangeSeatCommand> validator, CancellationToken ct) =>
        {
            var command = new ChangeSeatCommand(id, body.FromSeat, body.ToSeat);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("ChangeSeat")
        .WithSummary("Move a passenger to a different free seat on the same trip (no price change).");

        group.MapGet("/promo-preview", async (
            Guid tripId, string code, int? seats, PreviewPromoHandler handler,
            IValidator<PreviewPromoQuery> validator, CancellationToken ct) =>
        {
            var query = new PreviewPromoQuery(tripId, code, seats ?? 1);
            await validator.ValidateAndThrowAsync(query, ct);
            return Results.Ok(await handler.HandleAsync(query, ct));
        })
        .WithName("PreviewPromo")
        .WithSummary("Preview a promo code's discount on a trip before booking.");

        return app;
    }
}
