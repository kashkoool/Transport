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
        // Near-static — safe to cache briefly at the edge/CDN to take load off the DB.
        app.MapGet("/api/companies", async (HttpResponse response, ListPublicCompaniesHandler handler, CancellationToken ct) =>
        {
            response.Headers.CacheControl = "public, max-age=300";
            return Results.Ok(await handler.HandleAsync(ct));
        })
        .RequireRateLimiting(RateLimitPolicies.PublicRead)
        .WithTags("Trips")
        .CacheOutput("PublicCompanies")
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
        .CacheOutput("TripSearch")
        .WithName("SearchTrips")
        .WithSummary("Search bookable trips by route and date (optional company/maxPrice/departAfter).");

        // Public: the seat layout + currently-unavailable seats, for rendering the seat picker.
        group.MapGet("/{tripId:guid}/seat-map", async (
            Guid tripId, GetSeatMapHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(tripId, ct)))
        .WithName("GetTripSeatMap")
        .WithSummary("Seat layout and taken seats for a trip.");

        // Public: the ordered waypoints between origin and destination. Rarely changes after a trip
        // is set up — a short cache is safe. (Seat-map is deliberately NOT cached: it changes live.)
        group.MapGet("/{tripId:guid}/stops", async (
            HttpResponse response, Guid tripId, ListTripStopsHandler handler, CancellationToken ct) =>
        {
            response.Headers.CacheControl = "public, max-age=120";
            return Results.Ok(await handler.HandleAsync(tripId, ct));
        })
        .CacheOutput("TripStops")
        .WithName("GetTripStops")
        .WithSummary("Ordered intermediate stops for a trip.");

        return app;
    }
}
