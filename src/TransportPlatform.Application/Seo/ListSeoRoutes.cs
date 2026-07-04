using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.Application.Seo;

/// <summary>One SEO-indexable route (a distinct origin→destination pair) with its cheapest fare.</summary>
public sealed record RouteSummary(
    string Origin,
    string Destination,
    string Slug,
    int TripCount,
    decimal MinPrice,
    string Currency);

/// <summary>
/// Distinct routes that have at least one UPCOMING Scheduled trip on an ACTIVE company, with the
/// trip count and cheapest fare per route. Drives the sitemap, prerender list and routes-index page.
/// Grouped/aggregated entirely in SQL (no full entities loaded); ordered by popularity (trip count).
/// The Scheduled filter + <c>lower()</c> comparisons let this ride the route-search functional index.
/// </summary>
public sealed class ListSeoRoutesHandler(IApplicationDbContext db, IClock clock)
{
    // A public bus network has a bounded set of city pairs; cap so the query stays bounded no matter
    // how the trip table grows. Comfortably above any realistic distinct-route count.
    private const int MaxResults = 1000;

    public async Task<IReadOnlyList<RouteSummary>> HandleAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;

        var activeCompanyIds = db.Companies
            .Where(c => c.Status == CompanyStatus.Active)
            .Select(c => c.Id);

        // Aggregate in SQL per (Origin, Destination, Currency): count trips + min fare. Currency is part
        // of the key so Min(Price) is only ever taken within a single currency (never comparing SYP to
        // USD). Almost every route is single-currency, so this is normally one row per route already.
        var rows = await db.Trips
            .Where(t => t.Status == TripStatus.Scheduled
                        && t.DepartureUtc > now
                        && activeCompanyIds.Contains(t.CompanyId))
            .GroupBy(t => new { t.Origin, t.Destination, t.Currency })
            .Select(g => new
            {
                g.Key.Origin,
                g.Key.Destination,
                g.Key.Currency,
                TripCount = g.Count(),
                MinPrice = g.Min(t => t.Price),
            })
            .Take(MaxResults)
            .ToListAsync(ct);

        // Fold any multi-currency route into a single row: sum the trip counts, and report the cheapest
        // fare together with ITS currency. One row per distinct (Origin, Destination), popular first.
        return rows
            .GroupBy(x => new { x.Origin, x.Destination })
            .Select(g =>
            {
                var cheapest = g.OrderBy(x => x.MinPrice).First();
                return new RouteSummary(
                    g.Key.Origin, g.Key.Destination, Slug.Route(g.Key.Origin, g.Key.Destination),
                    g.Sum(x => x.TripCount), cheapest.MinPrice, cheapest.Currency);
            })
            .OrderByDescending(r => r.TripCount)
            .ThenBy(r => r.Slug, StringComparer.Ordinal)
            .ToList();
    }
}
