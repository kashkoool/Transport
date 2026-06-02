using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Application.Identity;

public sealed record RequestPasswordResetCommand(string Email);

/// <summary>
/// Starts a password reset: if the email maps to a user, emails a reset link. Returns nothing
/// and never reveals whether the account exists — the endpoint always responds generically
/// (anti-enumeration).
/// </summary>
public sealed class RequestPasswordResetHandler(IIdentityService identity, IAuthEmailService authEmail)
{
    public async Task HandleAsync(RequestPasswordResetCommand command, CancellationToken ct)
    {
        // No validator on this command (anti-enumeration endpoint); guard before Trim().
        if (string.IsNullOrWhiteSpace(command.Email))
            return;
        var email = command.Email.Trim();
        var created = await identity.CreatePasswordResetTokenAsync(email, ct);
        if (created is { } reset)
            await authEmail.SendPasswordResetAsync(email, reset.Token, ct);
    }
}
