using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Companies;

// ── Vendor: view + edit own company ─────────────────────────────────────────────

public sealed class GetMyCompanyHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<CompanyDto> HandleAsync(CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new NotFoundException("Company", companyId);
        return CompanyDto.From(company);
    }
}

public sealed record UpdateMyCompanyCommand(string Name, string? Phone,
    string? TaxId = null, string? BankAccount = null, string? Address = null);

public sealed class UpdateMyCompanyValidator : AbstractValidator<UpdateMyCompanyCommand>
{
    public UpdateMyCompanyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.TaxId).MaximumLength(50);
        RuleFor(x => x.BankAccount).MaximumLength(64);
        RuleFor(x => x.Address).MaximumLength(200);
    }
}

public sealed class UpdateMyCompanyHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<CompanyDto> HandleAsync(UpdateMyCompanyCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new NotFoundException("Company", companyId);
        company.Update(command.Name, command.Phone, command.TaxId, command.BankAccount, command.Address);
        await db.SaveChangesAsync(ct);
        return CompanyDto.From(company);
    }
}

// ── Admin: edit + delete any company ────────────────────────────────────────────

public sealed record UpdateCompanyCommand(Guid CompanyId, string Name, string? Phone,
    string? TaxId = null, string? BankAccount = null, string? Address = null);

public sealed class UpdateCompanyValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyValidator()
    {
        RuleFor(x => x.CompanyId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.TaxId).MaximumLength(50);
        RuleFor(x => x.BankAccount).MaximumLength(64);
        RuleFor(x => x.Address).MaximumLength(200);
    }
}

public sealed class UpdateCompanyHandler(IApplicationDbContext db)
{
    public async Task<CompanyDto> HandleAsync(UpdateCompanyCommand command, CancellationToken ct)
    {
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == command.CompanyId, ct)
            ?? throw new NotFoundException("Company", command.CompanyId);
        company.Update(command.Name, command.Phone, command.TaxId, command.BankAccount, command.Address);
        await db.SaveChangesAsync(ct);
        return CompanyDto.From(company);
    }
}

public sealed record DeleteCompanyCommand(Guid CompanyId);

/// <summary>
/// Admin deletes a company that has no operational data (buses/trips/drivers — the FKs are Restrict
/// anyway). Its manager/staff accounts are removed first so none are left orphaned.
/// </summary>
public sealed class DeleteCompanyHandler(IApplicationDbContext db, IIdentityService identity)
{
    public async Task HandleAsync(DeleteCompanyCommand command, CancellationToken ct)
    {
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == command.CompanyId, ct)
            ?? throw new NotFoundException("Company", command.CompanyId);

        if (await db.Buses.AnyAsync(b => b.CompanyId == company.Id, ct)
            || await db.Trips.AnyAsync(t => t.CompanyId == company.Id, ct)
            || await db.Drivers.AnyAsync(d => d.CompanyId == company.Id, ct))
            throw new ConflictException("company.in_use",
                "Remove the company's buses, trips and drivers before deleting it.");

        await identity.DeleteCompanyUsersAsync(company.Id, ct);
        db.Companies.Remove(company);
        await db.SaveChangesAsync(ct);
    }
}
