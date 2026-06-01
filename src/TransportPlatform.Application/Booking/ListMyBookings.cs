using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Bookings;

public sealed record ListMyBookingsQuery;

public sealed record BookingSummary(
    Guid BookingId,
    string Reference,
    string Status,
    string Origin,
    string Destination,
    DateTimeOffset DepartureUtc,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Lists the authenticated customer's bookings (newest first), joined to their trip for the
/// route + departure. Scoped to the caller's email from the JWT, so a customer only ever sees
/// their own bookings. Bounded result set (DB1): a single customer realistically has a modest
/// history; cap it so the query stays bounded as the table grows.
/// </summary>
public sealed class ListMyBookingsHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    private const int MaxResults = 100;

    public async Task<IReadOnlyList<BookingSummary>> HandleAsync(ListMyBookingsQuery query, CancellationToken ct)
    {
        var email = currentUser.RequireEmail();

        // Single join query (no N+1). Project the enum out and stringify client-side so the
        // status mapping never depends on the provider translating Enum.ToString().
        var rows = await (
            from b in db.Bookings
            join t in db.Trips on b.TripId equals t.Id
            where b.CustomerEmail == email
            orderby b.CreatedAtUtc descending
            select new
            {
                b.Id,
                b.Reference,
                b.Status,
                t.Origin,
                t.Destination,
                t.DepartureUtc,
                b.TotalAmount,
                b.Currency,
                b.CreatedAtUtc,
            })
            .Take(MaxResults)
            .ToListAsync(ct);

        return rows.Select(r => new BookingSummary(
            r.Id, r.Reference, r.Status.ToString(), r.Origin, r.Destination,
            r.DepartureUtc, r.TotalAmount, r.Currency, r.CreatedAtUtc)).ToList();
    }
}
