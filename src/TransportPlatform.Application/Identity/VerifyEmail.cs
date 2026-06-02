using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Application.Identity;

public sealed record VerifyEmailCommand(string Email, string Token);

/// <summary>Confirms an email with its token. Returns false on unknown user or invalid token.</summary>
public sealed class VerifyEmailHandler(IIdentityService identity)
{
    public Task<bool> HandleAsync(VerifyEmailCommand command, CancellationToken ct) =>
        // The endpoint has no validator, so guard a missing email here rather than NRE on Trim().
        string.IsNullOrWhiteSpace(command.Email)
            ? Task.FromResult(false)
            : identity.ConfirmEmailAsync(command.Email.Trim(), command.Token, ct);
}
