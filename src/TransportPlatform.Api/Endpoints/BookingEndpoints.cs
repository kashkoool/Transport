using FluentValidation;
using TransportPlatform.Application.Bookings;

namespace TransportPlatform.Api.Endpoints;

public static class BookingEndpoints
{
    public sealed record HoldRequest(Guid TripId, IReadOnlyList<int> SeatNumbers, string HeldBy);
    public sealed record CreateBookingRequest(
        Guid TripId, string CustomerEmail, string HeldBy, IReadOnlyList<PassengerInput> Passengers);

    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bookings").WithTags("Bookings");

        group.MapPost("/hold", async (
            HoldRequest body, HoldSeatsHandler handler,
            IValidator<HoldSeatsCommand> validator, CancellationToken ct) =>
        {
            var command = new HoldSeatsCommand(body.TripId, body.SeatNumbers, body.HeldBy);
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

            var command = new CreateBookingCommand(
                body.TripId, body.CustomerEmail, body.HeldBy, body.Passengers, idempotencyKey);
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
        .WithSummary("Get the boarding ticket (with QR payload) for a booking.");

        return app;
    }
}
