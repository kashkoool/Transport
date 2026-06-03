using FluentValidation;
using TransportPlatform.Application.Trips;

namespace TransportPlatform.Api.Endpoints;

public static class TripEndpoints
{
    public static IEndpointRouteBuilder MapTripEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/trips").WithTags("Trips");

        // Public: browse trips on a route/date.
        group.MapGet("/search", async (
            string origin, string destination, DateOnly date,
            SearchTripsHandler handler, IValidator<SearchTripsQuery> validator, CancellationToken ct) =>
        {
            var query = new SearchTripsQuery(origin, destination, date);
            await validator.ValidateAndThrowAsync(query, ct);
            var results = await handler.HandleAsync(query, ct);
            return Results.Ok(results);
        })
        .WithName("SearchTrips")
        .WithSummary("Search bookable trips by route and date.");

        // Public: the seat layout + currently-unavailable seats, for rendering the seat picker.
        group.MapGet("/{tripId:guid}/seat-map", async (
            Guid tripId, GetSeatMapHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(tripId, ct)))
        .WithName("GetTripSeatMap")
        .WithSummary("Seat layout and taken seats for a trip.");

        // Public: the ordered waypoints between origin and destination.
        group.MapGet("/{tripId:guid}/stops", async (
            Guid tripId, ListTripStopsHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(tripId, ct)))
        .WithName("GetTripStops")
        .WithSummary("Ordered intermediate stops for a trip.");

        return app;
    }
}
