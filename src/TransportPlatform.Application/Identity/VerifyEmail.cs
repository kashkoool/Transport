using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Application.Identity;

public sealed record VerifyEmailCommand(string Email, string Token);

/// <summary>Confirms an email with its token. Returns false on unknown user or invalid token.</summary>
public sealed class VerifyEmailHandler(IIdentityService identity)
{
    public Task<bool> HandleAsync(VerifyEmailCommand command, CancellationToken ct) =>
        identity.ConfirmEmailAsync(command.Email.Trim(), command.Token, ct);
}
