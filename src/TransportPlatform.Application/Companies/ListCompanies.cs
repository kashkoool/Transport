using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Companies;

namespace TransportPlatform.Application.Companies;

public sealed record ListCompaniesQuery(int? Page, int? Limit, CompanyStatus? Status);

/// <summary>Admin paginated list of vendor companies, newest first, optionally by status.</summary>
public sealed class ListCompaniesHandler(IApplicationDbContext db)
{
    public async Task<PagedResult<CompanyDto>> HandleAsync(ListCompaniesQuery query, CancellationToken ct)
    {
        var page = new PageRequest(query.Page, query.Limit);

        var companies = db.Companies.AsQueryable();
        if (query.Status is { } status)
            companies = companies.Where(c => c.Status == status);

        var total = await companies.CountAsync(ct);
        var items = await companies
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip(page.Skip)
            .Take(page.Limit)
            .Select(c => new CompanyDto(c.Id, c.Name, c.Email, c.Phone, c.Status.ToString()))
            .ToListAsync(ct);

        return new PagedResult<CompanyDto>(items, total, page.Page, page.Limit);
    }
}
