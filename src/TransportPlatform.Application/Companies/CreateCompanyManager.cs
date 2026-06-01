using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Companies;

public sealed record CreateCompanyManagerCommand(Guid CompanyId, string Email, string Password, string FullName);

public sealed class CreateCompanyManagerValidator : AbstractValidator<CreateCompanyManagerCommand>
{
    public CreateCompanyManagerValidator()
    {
        RuleFor(x => x.CompanyId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
    }
}

/// <summary>
/// Admin creates the login for a vendor company's manager, bound to that company. The
/// company_id is stamped on the account so every vendor action is automatically scoped.
/// </summary>
public sealed class CreateCompanyManagerHandler(IApplicationDbContext db, IIdentityService identity)
{
    public async Task<CompanyManagerDto> HandleAsync(CreateCompanyManagerCommand command, CancellationToken ct)
    {
        var companyExists = await db.Companies.AnyAsync(c => c.Id == command.CompanyId, ct);
        if (!companyExists)
            throw new NotFoundException("Company", command.CompanyId);

        var user = await identity.RegisterVendorManagerAsync(
            command.Email, command.Password, command.FullName, command.CompanyId, ct);

        return new CompanyManagerDto(user.UserId, user.Email, command.CompanyId);
    }
}
