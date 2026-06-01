using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Trips;

public sealed record ListVendorTripsQuery(int? Page, int? Limit);

/// <summary>Lists the calling vendor's own trips (scoped to their company id), newest departure first.</summary>
public sealed class ListVendorTripsHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<PagedResult<TripDto>> HandleAsync(ListVendorTripsQuery query, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var page = new PageRequest(query.Page, query.Limit);

        var trips = db.Trips.Where(t => t.CompanyId == companyId);

        var total = await trips.CountAsync(ct);
        var items = await trips
            .OrderByDescending(t => t.DepartureUtc)
            .Skip(page.Skip)
            .Take(page.Limit)
            .Select(t => new TripDto(t.Id, t.BusId, t.Origin, t.Destination, t.DepartureUtc,
                t.ArrivalUtc, t.SeatCount, t.Price, t.Currency, t.Status.ToString()))
            .ToListAsync(ct);

        return new PagedResult<TripDto>(items, total, page.Page, page.Limit);
    }
}
