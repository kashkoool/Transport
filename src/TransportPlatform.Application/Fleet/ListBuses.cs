using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Fleet;

public sealed record ListBusesQuery(int? Page, int? Limit, string? Search = null);

/// <summary>Lists the calling vendor's own fleet (scoped to their company id).</summary>
public sealed class ListBusesHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<PagedResult<BusDto>> HandleAsync(ListBusesQuery query, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var page = new PageRequest(query.Page, query.Limit);

        var buses = db.Buses.Where(b => b.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            // ToLower()/Contains run server-side as SQL lower()/LIKE inside this EF expression tree,
            // so the culture/StringComparison analyzers are false positives here.
#pragma warning disable CA1304, CA1311, CA1862
            buses = buses.Where(b => b.BusNumber.ToLower().Contains(term)
                || (b.Model != null && b.Model.ToLower().Contains(term)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var total = await buses.CountAsync(ct);
        var items = await buses
            .OrderBy(b => b.BusNumber)
            .Skip(page.Skip)
            .Take(page.Limit)
            .Select(b => new BusDto(b.Id, b.BusNumber, b.SeatCount, b.Type.ToString(), b.Model, b.DriverId, b.SeatsPerRow))
            .ToListAsync(ct);

        return new PagedResult<BusDto>(items, total, page.Page, page.Limit);
    }
}
