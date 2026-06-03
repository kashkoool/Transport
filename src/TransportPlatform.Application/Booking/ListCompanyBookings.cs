using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Bookings;

public sealed record ListCompanyBookingsQuery(int? Page, int? Limit);

public sealed record CompanyBookingDto(
    Guid BookingId, string Reference, string CustomerEmail, string Status,
    decimal TotalAmount, string Currency, string Origin, string Destination,
    DateTimeOffset DepartureUtc, DateTimeOffset CreatedAtUtc);

/// <summary>Lists bookings for the operator's company (newest first), for the staff desk view.</summary>
public sealed class ListCompanyBookingsHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<PagedResult<CompanyBookingDto>> HandleAsync(ListCompanyBookingsQuery query, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var page = new PageRequest(query.Page, query.Limit);

        var scoped =
            from b in db.Bookings
            join t in db.Trips on b.TripId equals t.Id
            where t.CompanyId == companyId
            select new { b, t };

        var total = await scoped.CountAsync(ct);
        var items = await scoped
            .OrderByDescending(x => x.b.CreatedAtUtc)
            .ThenByDescending(x => x.b.Id)
            .Skip(page.Skip)
            .Take(page.Limit)
            .Select(x => new CompanyBookingDto(
                x.b.Id, x.b.Reference, x.b.CustomerEmail, x.b.Status.ToString(),
                x.b.TotalAmount, x.b.Currency, x.t.Origin, x.t.Destination,
                x.t.DepartureUtc, x.b.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<CompanyBookingDto>(items, total, page.Page, page.Limit);
    }
}
