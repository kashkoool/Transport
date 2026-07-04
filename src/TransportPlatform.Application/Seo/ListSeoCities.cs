using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.Application.Seo;

/// <summary>A city that appears on the network, with how many distinct routes touch it.</summary>
public sealed record CitySummary(string Name, string Slug, int RouteCount);

/// <summary>
/// Distinct cities appearing as Origin OR Destination on upcoming Scheduled trips of Active companies,
/// each with the number of DISTINCT routes it participates in. Distinct routes are computed in SQL,
/// then folded per-city in memory (the distinct-route set is bounded — one row per city pair).
/// </summary>
public sealed class ListSeoCitiesHandler(IApplicationDbContext db, IClock clock)
{
    private const int MaxRoutes = 2000;

    public async Task<IReadOnlyList<CitySummary>> HandleAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;

        var activeCompanyIds = db.Companies
            .Where(c => c.Status == CompanyStatus.Active)
            .Select(c => c.Id);

        // Distinct routes only (SQL DISTINCT), so counting routes-per-city is unaffected by how many
        // trips run on each route.
        var routes = await db.Trips
            .Where(t => t.Status == TripStatus.Scheduled
                        && t.DepartureUtc > now
                        && activeCompanyIds.Contains(t.CompanyId))
            .Select(t => new { t.Origin, t.Destination })
            .Distinct()
            .Take(MaxRoutes)
            .ToListAsync(ct);

        // A city's route count = number of distinct routes where it is either endpoint. Case-insensitive
        // fold on the trimmed display name, keeping the first-seen casing for display.
        var byCity = new Dictionary<string, (string Display, HashSet<string> Routes)>(StringComparer.OrdinalIgnoreCase);

        void Touch(string city, string routeKey)
        {
            var key = city.Trim();
            if (!byCity.TryGetValue(key, out var entry))
            {
                entry = (key, new HashSet<string>(StringComparer.Ordinal));
                byCity[key] = entry;
            }

            entry.Routes.Add(routeKey);
        }

        foreach (var r in routes)
        {
            var routeKey = Slug.Route(r.Origin, r.Destination);
            Touch(r.Origin, routeKey);
            Touch(r.Destination, routeKey);
        }

        return byCity.Values
            .Select(e => new CitySummary(e.Display, Slug.City(e.Display), e.Routes.Count))
            .OrderByDescending(c => c.RouteCount)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
