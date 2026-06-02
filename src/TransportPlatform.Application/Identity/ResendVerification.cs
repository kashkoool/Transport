using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Application.Identity;

public sealed record ResendVerificationCommand(string Email);

/// <summary>
/// Re-sends the email-verification link if the address maps to an unverified user. Reveals
/// nothing — the endpoint always responds generically (anti-enumeration).
/// </summary>
public sealed class ResendVerificationHandler(IIdentityService identity, IAuthEmailService authEmail)
{
    public async Task HandleAsync(ResendVerificationCommand command, CancellationToken ct)
    {
        // No validator on this command (anti-enumeration endpoint); guard before Trim().
        if (string.IsNullOrWhiteSpace(command.Email))
            return;
        var email = command.Email.Trim();
        var created = await identity.CreateEmailVerificationTokenAsync(email, ct);
        if (created is { } verification)
            await authEmail.SendEmailVerificationAsync(email, verification.Token, ct);
    }
}
