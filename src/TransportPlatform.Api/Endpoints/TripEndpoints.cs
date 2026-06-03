using FluentValidation;
using TransportPlatform.Api.Security;
using TransportPlatform.Application.Companies;
using TransportPlatform.Application.Trips;

namespace TransportPlatform.Api.Endpoints;

public static class TripEndpoints
{
    public static IEndpointRouteBuilder MapTripEndpoints(this IEndpointRouteBuilder app)
    {
        // Anonymous reads — bounded by a dedicated per-IP tier, not just the shared global limiter.
        var group = app.MapGroup("/api/trips").WithTags("Trips")
            .RequireRateLimiting(RateLimitPolicies.PublicRead);

        // Public: active companies (id + name) for the search "filter by company" control.
        app.MapGet("/api/companies", async (ListPublicCompaniesHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)))
        .RequireRateLimiting(RateLimitPolicies.PublicRead)
        .WithTags("Trips")
        .WithName("ListPublicCompanies")
        .WithSummary("List active companies (id + name) for trip search filtering.");

        // Public: browse trips on a route/date, with optional company/price/time filters.
        group.MapGet("/search", async (
            string origin, string destination, DateOnly date,
            Guid? companyId, decimal? maxPrice, TimeOnly? departAfter,
            SearchTripsHandler handler, IValidator<SearchTripsQuery> validator, CancellationToken ct) =>
        {
            var query = new SearchTripsQuery(origin, destination, date, companyId, maxPrice, departAfter);
            await validator.ValidateAndThrowAsync(query, ct);
            var results = await handler.HandleAsync(query, ct);
            return Results.Ok(results);
        })
        .WithName("SearchTrips")
        .WithSummary("Search bookable trips by route and date (optional company/maxPrice/departAfter).");

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
