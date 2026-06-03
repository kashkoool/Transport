using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Fleet;

public sealed record ListDriversQuery(int? Page, int? Limit);

/// <summary>Lists the calling vendor's own drivers (scoped to their company id).</summary>
public sealed class ListDriversHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<PagedResult<DriverDto>> HandleAsync(ListDriversQuery query, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var page = new PageRequest(query.Page, query.Limit);

        var drivers = db.Drivers.Where(d => d.CompanyId == companyId);
        var total = await drivers.CountAsync(ct);
        var items = await drivers
            .OrderBy(d => d.FullName)
            .Skip(page.Skip)
            .Take(page.Limit)
            .Select(d => new DriverDto(d.Id, d.FullName, d.Phone, d.LicenseNumber))
            .ToListAsync(ct);

        return new PagedResult<DriverDto>(items, total, page.Page, page.Limit);
    }
}
