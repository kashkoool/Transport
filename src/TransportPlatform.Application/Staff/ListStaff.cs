using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Staff;

public sealed record ListStaffQuery(int? Page, int? Limit, string? Search = null);

/// <summary>Lists the calling manager's own company staff (scoped to their company id).</summary>
public sealed class ListStaffHandler(IIdentityService identity, ICurrentUser currentUser)
{
    public async Task<PagedResult<StaffDto>> HandleAsync(ListStaffQuery query, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var page = new PageRequest(query.Page, query.Limit);

        var total = await identity.CountStaffAsync(companyId, query.Search, ct);
        var members = await identity.ListStaffAsync(companyId, page.Skip, page.Limit, query.Search, ct);
        var items = members
            .Select(m => new StaffDto(m.Id, m.Email, m.FullName, m.StaffType, m.Suspended))
            .ToList();

        return new PagedResult<StaffDto>(items, total, page.Page, page.Limit);
    }
}
