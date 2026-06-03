using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Fleet;

public sealed record ListDriversQuery(int? Page, int? Limit, string? Search = null);

/// <summary>Lists the calling vendor's own drivers (scoped to their company id).</summary>
public sealed class ListDriversHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<PagedResult<DriverDto>> HandleAsync(ListDriversQuery query, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var page = new PageRequest(query.Page, query.Limit);

        var drivers = db.Drivers.Where(d => d.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            // Server-side SQL lower()/LIKE inside this EF expression tree → analyzers are false positives.
#pragma warning disable CA1304, CA1311, CA1862
            drivers = drivers.Where(d => d.FullName.ToLower().Contains(term)
                || (d.Phone != null && d.Phone.ToLower().Contains(term)));
#pragma warning restore CA1304, CA1311, CA1862
        }
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
