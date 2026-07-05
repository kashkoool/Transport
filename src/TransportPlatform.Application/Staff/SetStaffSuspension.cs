using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Staff;

public sealed record SetStaffSuspendedCommand(Guid StaffId, bool Suspended);

/// <summary>
/// Suspend or reactivate one of the manager's own staff. The company id from the caller scopes
/// the action, so a manager can never lock out another company's staff.
/// </summary>
public sealed class SetStaffSuspendedHandler(IIdentityService identity, ICurrentUser currentUser, ITokenService tokens)
{
    public async Task HandleAsync(SetStaffSuspendedCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var ok = await identity.SetStaffSuspendedAsync(companyId, command.StaffId, command.Suspended, ct);
        if (!ok)
            throw new NotFoundException("Staff", command.StaffId);

        // Suspending must sever any live sessions: kill all refresh tokens so the staff account
        // can't keep refreshing an access token after being locked out.
        if (command.Suspended)
            await tokens.RevokeAllForUserAsync(command.StaffId, ct);
    }
}
