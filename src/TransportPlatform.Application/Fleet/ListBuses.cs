using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Fleet;

public sealed record ListBusesQuery(int? Page, int? Limit);

/// <summary>Lists the calling vendor's own fleet (scoped to their company id).</summary>
public sealed class ListBusesHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<PagedResult<BusDto>> HandleAsync(ListBusesQuery query, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var page = new PageRequest(query.Page, query.Limit);

        var buses = db.Buses.Where(b => b.CompanyId == companyId);

        var total = await buses.CountAsync(ct);
        var items = await buses
            .OrderBy(b => b.BusNumber)
            .Skip(page.Skip)
            .Take(page.Limit)
            .Select(b => new BusDto(b.Id, b.BusNumber, b.SeatCount, b.Type.ToString(), b.Model))
            .ToListAsync(ct);

        return new PagedResult<BusDto>(items, total, page.Page, page.Limit);
    }
}
