using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Staff;

public sealed record SetStaffSuspendedCommand(Guid StaffId, bool Suspended);

/// <summary>
/// Suspend or reactivate one of the manager's own staff. The company id from the caller scopes
/// the action, so a manager can never lock out another company's staff.
/// </summary>
public sealed class SetStaffSuspendedHandler(IIdentityService identity, ICurrentUser currentUser)
{
    public async Task HandleAsync(SetStaffSuspendedCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var ok = await identity.SetStaffSuspendedAsync(companyId, command.StaffId, command.Suspended, ct);
        if (!ok)
            throw new NotFoundException("Staff", command.StaffId);
    }
}
