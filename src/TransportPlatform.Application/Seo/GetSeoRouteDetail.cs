using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.Application.Seo;

/// <summary>A single upcoming departure on a route, for the route landing page's "next departures" list.</summary>
public sealed record NextDeparture(
    DateTimeOffset DepartureUtc,
    DateTimeOffset ArrivalUtc,
    decimal Price,
    string Currency,
    string CompanyName);

/// <summary>SEO landing-page detail for one route (origin→destination).</summary>
public sealed record RouteDetail(
    string Origin,
    string Destination,
    string Slug,
    decimal MinPrice,
    string Currency,
    int UpcomingCount,
    string[] Companies,
    int? AvgDurationMinutes,
    NextDeparture[] Next);

/// <summary>
/// Builds the SEO landing page for a route. The slug is resolved back to a real (Origin, Destination)
/// pair via the DB's DISTINCT upcoming routes (never a blind string split, since a city may contain a
/// hyphen). Returns 404 (<see cref="NotFoundException"/>) when no upcoming Scheduled/Active trips match.
/// All reads filter Status==Scheduled and compare on lower() so they use the route-search index.
/// </summary>
public sealed class GetSeoRouteDetailHandler(IApplicationDbContext db, IClock clock)
{
    private const int MaxNext = 10;

    public async Task<RouteDetail> HandleAsync(string slug, CancellationToken ct)
    {
        var now = clock.UtcNow;

        var activeCompanyIds = db.Companies
            .Where(c => c.Status == CompanyStatus.Active)
            .Select(c => c.Id);

        // Base filter: upcoming, Scheduled, on an active company. Reused for slug resolution + reads.
        var upcoming = db.Trips
            .Where(t => t.Status == TripStatus.Scheduled
                        && t.DepartureUtc > now
                        && activeCompanyIds.Contains(t.CompanyId));

        // Resolve the slug against the DB's distinct routes rather than splitting the string.
        var distinctRoutes = await upcoming
            .Select(t => new { t.Origin, t.Destination })
            .Distinct()
            .ToListAsync(ct);

        if (!Slug.TryParseRoute(slug, distinctRoutes.Select(r => (r.Origin, r.Destination)), out var origin, out var destination))
            throw new NotFoundException("Route", slug);

        var originLower = origin.ToLowerInvariant();
        var destinationLower = destination.ToLowerInvariant();

#pragma warning disable CA1304, CA1311, CA1862 // lower() runs server-side; see SearchTrips.cs
        var routeTrips = upcoming
            .Where(t => t.Origin.ToLower() == originLower && t.Destination.ToLower() == destinationLower);

        // Count + cheapest fare aggregated in SQL (both trivially translatable).
        var stats = await routeTrips
            .GroupBy(_ => 1)
            .Select(g => new
            {
                UpcomingCount = g.Count(),
                MinPrice = g.Min(t => t.Price),
            })
            .SingleOrDefaultAsync(ct);

        if (stats is null || stats.UpcomingCount == 0)
            throw new NotFoundException("Route", slug);

        // Currency for the min fare (the route may in theory mix currencies; pick the cheapest fare's).
        var currency = await routeTrips
            .OrderBy(t => t.Price)
            .Select(t => t.Currency)
            .FirstAsync(ct);

        var companies = await routeTrips
            .Select(t => t.CompanyId)
            .Distinct()
            .Join(db.Companies, id => id, c => c.Id, (_, c) => c.Name)
            .OrderBy(name => name)
            .ToArrayAsync(ct);

        // Average duration: project just the two timestamps and average in memory. The route's upcoming
        // trip set is already bounded by the Scheduled/Active/upcoming filter, and this avoids relying on
        // provider-specific interval-aggregate translation.
        var durationsMinutes = await routeTrips
            .Select(t => new { t.DepartureUtc, t.ArrivalUtc })
            .ToListAsync(ct);

        var next = await routeTrips
            .OrderBy(t => t.DepartureUtc)
            .Take(MaxNext)
            .Join(db.Companies, t => t.CompanyId, c => c.Id,
                (t, c) => new NextDeparture(t.DepartureUtc, t.ArrivalUtc, t.Price, t.Currency, c.Name))
            .ToArrayAsync(ct);
#pragma warning restore CA1304, CA1311, CA1862

        int? avgMinutes = durationsMinutes.Count > 0
            ? (int)Math.Round(durationsMinutes.Average(d => (d.ArrivalUtc - d.DepartureUtc).TotalMinutes))
            : null;

        return new RouteDetail(
            origin, destination, Slug.Route(origin, destination),
            stats.MinPrice, currency, stats.UpcomingCount, companies, avgMinutes, next);
    }
}
