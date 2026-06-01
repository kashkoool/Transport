using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Companies;

public sealed record SetCompanyStatusCommand(Guid CompanyId, bool Activate);

/// <summary>
/// Admin activates or suspends a vendor company. Suspending blocks that company's managers
/// at the data layer (their trips stop being bookable once the rule is wired into search).
/// </summary>
public sealed class SetCompanyStatusHandler(IApplicationDbContext db)
{
    public async Task<CompanyDto> HandleAsync(SetCompanyStatusCommand command, CancellationToken ct)
    {
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == command.CompanyId, ct)
                      ?? throw new NotFoundException("Company", command.CompanyId);

        if (command.Activate)
            company.Activate();
        else
            company.Suspend();

        await db.SaveChangesAsync(ct);
        return CompanyDto.From(company);
    }
}
