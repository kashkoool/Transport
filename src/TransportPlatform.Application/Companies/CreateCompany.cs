using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Companies;

namespace TransportPlatform.Application.Companies;

public sealed record CreateCompanyCommand(string Name, string Email, string? Phone);

public sealed class CreateCompanyValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Phone).MaximumLength(40);
    }
}

/// <summary>Admin onboards a new vendor company (starts in Pending until activated).</summary>
public sealed class CreateCompanyHandler(IApplicationDbContext db)
{
    public async Task<CompanyDto> HandleAsync(CreateCompanyCommand command, CancellationToken ct)
    {
        var company = new Company(command.Name, command.Email, command.Phone);
        db.Companies.Add(company);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique index on company email.
            throw new ConflictException("company.email_taken", "A company with this email already exists.");
        }

        return CompanyDto.From(company);
    }
}
