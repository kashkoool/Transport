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
            SearchTripsHandler handler, CancellationToken ct) =>
        {
            var results = await handler.HandleAsync(new SearchTripsQuery(origin, destination, date), ct);
            return Results.Ok(results);
        })
        .WithName("SearchTrips")
        .WithSummary("Search bookable trips by route and date.");

        return app;
    }
}
