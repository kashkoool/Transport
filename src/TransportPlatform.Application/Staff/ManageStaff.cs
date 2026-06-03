using FluentValidation;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Identity;

namespace TransportPlatform.Application.Staff;

public sealed record UpdateStaffCommand(Guid StaffId, string FullName, StaffType StaffType);

public sealed class UpdateStaffValidator : AbstractValidator<UpdateStaffCommand>
{
    public UpdateStaffValidator()
    {
        RuleFor(x => x.StaffId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.StaffType).IsInEnum();
    }
}

/// <summary>Edit one of the manager's own staff (name + sub-type), scoped to their company.</summary>
public sealed class UpdateStaffHandler(IIdentityService identity, ICurrentUser currentUser)
{
    public async Task HandleAsync(UpdateStaffCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var ok = await identity.UpdateStaffAsync(companyId, command.StaffId, command.FullName, command.StaffType, ct);
        if (!ok)
            throw new NotFoundException("Staff", command.StaffId);
    }
}

public sealed record DeleteStaffCommand(Guid StaffId);

/// <summary>Delete one of the manager's own staff accounts, scoped to their company.</summary>
public sealed class DeleteStaffHandler(IIdentityService identity, ICurrentUser currentUser)
{
    public async Task HandleAsync(DeleteStaffCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var ok = await identity.DeleteStaffAsync(companyId, command.StaffId, ct);
        if (!ok)
            throw new NotFoundException("Staff", command.StaffId);
    }
}
