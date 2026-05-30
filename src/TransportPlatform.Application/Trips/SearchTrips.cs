using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.Application.Trips;

public sealed record SearchTripsQuery(string Origin, string Destination, DateOnly Date);

public sealed record TripSummary(
    Guid Id,
    string Origin,
    string Destination,
    DateTimeOffset DepartureUtc,
    DateTimeOffset ArrivalUtc,
    decimal Price,
    string Currency,
    int SeatCount,
    int AvailableSeats);

/// <summary>
/// Finds bookable trips on a route/date and reports live availability
/// (capacity minus confirmed seats minus active holds) without N+1 queries.
/// </summary>
public sealed class SearchTripsHandler(IApplicationDbContext db, IClock clock)
{
    public async Task<IReadOnlyList<TripSummary>> HandleAsync(SearchTripsQuery query, CancellationToken ct)
    {
        var dayStart = new DateTimeOffset(query.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        var now = clock.UtcNow;

        var origin = query.Origin.Trim().ToLowerInvariant();
        var destination = query.Destination.Trim().ToLowerInvariant();

        // These string-comparison analyzers are false positives inside an EF expression tree:
        // t.Origin.ToLower() is translated to SQL lower() and runs server-side, so there is no
        // client culture involved (CA1304/CA1311), and the StringComparison overload CA1862
        // recommends cannot be translated to SQL at all. Suppressed only for this query.
#pragma warning disable CA1304, CA1311, CA1862
        var trips = await db.Trips
            .Where(t => t.Status == TripStatus.Scheduled
                        && t.DepartureUtc > now
                        && t.DepartureUtc >= dayStart
                        && t.DepartureUtc < dayEnd
                        && t.Origin.ToLower() == origin
                        && t.Destination.ToLower() == destination)
            .OrderBy(t => t.DepartureUtc)
            .ToListAsync(ct);
#pragma warning restore CA1304, CA1311, CA1862

        if (trips.Count == 0)
            return [];

        var tripIds = trips.Select(t => t.Id).ToList();

        var assignmentCounts = await db.SeatAssignments
            .Where(a => tripIds.Contains(a.TripId))
            .GroupBy(a => a.TripId)
            .Select(g => new { TripId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TripId, x => x.Count, ct);

        var holdCounts = await db.SeatHolds
            .Where(h => tripIds.Contains(h.TripId) && !h.Consumed && h.ExpiresAtUtc > now)
            .GroupBy(h => h.TripId)
            .Select(g => new { TripId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TripId, x => x.Count, ct);

        return trips.Select(t =>
        {
            var taken = assignmentCounts.GetValueOrDefault(t.Id) + holdCounts.GetValueOrDefault(t.Id);
            var available = Math.Max(0, t.SeatCount - taken);
            return new TripSummary(t.Id, t.Origin, t.Destination, t.DepartureUtc, t.ArrivalUtc,
                t.Price, t.Currency, t.SeatCount, available);
        }).ToList();
    }
}
