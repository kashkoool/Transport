using FluentValidation;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Identity;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        // Identity enforces the full complexity rules on apply; mirror the register minimum here.
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(10);
        RuleFor(x => x.NewPassword).NotEqual(x => x.CurrentPassword)
            .WithMessage("The new password must be different from the current one.");
    }
}

/// <summary>
/// Changes the authenticated caller's password (verifying the current one) and revokes all of
/// their refresh tokens so other sessions are invalidated. A wrong current password yields a
/// generic 401 — the same surface as any failed credential check.
/// </summary>
public sealed class ChangePasswordHandler(IIdentityService identity, ITokenService tokens, ICurrentUser currentUser)
{
    public async Task HandleAsync(ChangePasswordCommand command, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var ok = await identity.ChangePasswordAsync(userId, command.CurrentPassword, command.NewPassword, ct);
        if (!ok)
            throw new UnauthorizedException("auth.invalid_credentials", "The current password is incorrect.");

        await tokens.RevokeAllForUserAsync(userId, ct);
    }
}
