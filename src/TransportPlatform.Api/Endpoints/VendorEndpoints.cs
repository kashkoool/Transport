using FluentValidation;
using TransportPlatform.Api.Security;
using TransportPlatform.Application.Common;
using TransportPlatform.Application.Fleet;
using TransportPlatform.Application.Trips;
using TransportPlatform.Domain.Fleet;

namespace TransportPlatform.Api.Endpoints;

public static class VendorEndpoints
{
    public sealed record AddBusRequest(string BusNumber, int SeatCount, BusType Type, string? Model);
    public sealed record ScheduleTripRequest(
        Guid BusId, string Origin, string Destination,
        DateTimeOffset DepartureUtc, DateTimeOffset ArrivalUtc, decimal Price, string Currency);

    public static IEndpointRouteBuilder MapVendorEndpoints(this IEndpointRouteBuilder app)
    {
        // Vendor managers only. Every handler scopes to the caller's own company id.
        var group = app.MapGroup("/api/vendor").WithTags("Vendor")
            .RequireAuthorization(AuthorizationPolicies.VendorOnly)
            .RequireRateLimiting(RateLimitPolicies.Sensitive);

        // ── Fleet ───────────────────────────────────────────────────────────────────
        group.MapPost("/buses", async (
            AddBusRequest body, AddBusHandler handler,
            IValidator<AddBusCommand> validator, CancellationToken ct) =>
        {
            var command = new AddBusCommand(body.BusNumber, body.SeatCount, body.Type, body.Model);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("AddBus")
        .WithSummary("Add a bus to your fleet.");

        group.MapGet("/buses", async (
            int? page, int? limit, ListBusesHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListBusesQuery(page, limit), ct)))
        .WithName("ListBuses")
        .WithSummary("List your fleet (paginated).");

        // ── Trips ───────────────────────────────────────────────────────────────────
        group.MapPost("/trips", async (
            ScheduleTripRequest body, ScheduleTripHandler handler,
            IValidator<ScheduleTripCommand> validator, CancellationToken ct) =>
        {
            var command = new ScheduleTripCommand(body.BusId, body.Origin, body.Destination,
                body.DepartureUtc, body.ArrivalUtc, body.Price, body.Currency);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("ScheduleTrip")
        .WithSummary("Schedule a trip on one of your buses.");

        group.MapGet("/trips", async (
            int? page, int? limit, ListVendorTripsHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListVendorTripsQuery(page, limit), ct)))
        .WithName("ListVendorTrips")
        .WithSummary("List your trips (paginated).");

        group.MapPost("/trips/{id:guid}/cancel", async (
            Guid id, CancelTripHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new CancelTripCommand(id), ct)))
        .WithName("CancelTrip")
        .WithSummary("Cancel one of your trips.");

        return app;
    }
}
