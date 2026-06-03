using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Companies;

namespace TransportPlatform.Application.Companies;

/// <summary>A sellable company, for the customer's "filter by company" search control (no PII).</summary>
public sealed record PublicCompany(Guid Id, string Name);

/// <summary>
/// Lists active companies (id + name only) so the trip search can offer a company filter. Bounded
/// result set; only Active companies are sellable, so suspended/pending vendors never appear.
/// </summary>
public sealed class ListPublicCompaniesHandler(IApplicationDbContext db)
{
    private const int MaxResults = 200;

    public async Task<IReadOnlyList<PublicCompany>> HandleAsync(CancellationToken ct) =>
        await db.Companies
            .Where(c => c.Status == CompanyStatus.Active)
            .OrderBy(c => c.Name)
            .Take(MaxResults)
            .Select(c => new PublicCompany(c.Id, c.Name))
            .ToListAsync(ct);
}
