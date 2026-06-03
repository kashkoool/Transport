using FluentValidation;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Identity;

namespace TransportPlatform.Application.Staff;

public sealed record CreateStaffCommand(string Email, string Password, string FullName, StaffType StaffType);

public sealed class CreateStaffValidator : AbstractValidator<CreateStaffCommand>
{
    public CreateStaffValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.StaffType).IsInEnum();
    }
}

/// <summary>
/// A vendor manager creates a staff account in their OWN company. The company id comes from the
/// caller's identity (never the request), so staff can't be created for another tenant.
/// </summary>
public sealed class CreateStaffHandler(IIdentityService identity, ICurrentUser currentUser)
{
    public async Task<StaffDto> HandleAsync(CreateStaffCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var id = await identity.RegisterStaffAsync(
            companyId, command.Email, command.Password, command.FullName, command.StaffType, ct);
        return new StaffDto(id, command.Email, command.FullName, command.StaffType.ToString(), Suspended: false);
    }
}
