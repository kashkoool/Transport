using FluentValidation;
using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Application.Identity;

public sealed record ResetPasswordCommand(string Email, string Token, string NewPassword);

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        // Mirrors the register policy (Identity enforces the full complexity rules on apply).
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(10);
    }
}

/// <summary>
/// Applies a password reset for a valid email+token. On success, revokes all of the user's
/// refresh tokens so any existing sessions are invalidated. Returns false for an invalid or
/// expired token (the endpoint maps that to a generic failure).
/// </summary>
public sealed class ResetPasswordHandler(IIdentityService identity, ITokenService tokens)
{
    public async Task<bool> HandleAsync(ResetPasswordCommand command, CancellationToken ct)
    {
        var userId = await identity.ResetPasswordAsync(command.Email.Trim(), command.Token, command.NewPassword, ct);
        if (userId is not { } id)
            return false;

        await tokens.RevokeAllForUserAsync(id, ct);
        return true;
    }
}
