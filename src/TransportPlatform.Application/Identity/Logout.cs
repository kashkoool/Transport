using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Application.Identity;

public sealed record LogoutCommand(string RefreshToken);

/// <summary>Revokes a refresh token so it can no longer be rotated.</summary>
public sealed class LogoutHandler(ITokenService tokens)
{
    public Task HandleAsync(LogoutCommand command, CancellationToken ct) =>
        tokens.RevokeAsync(command.RefreshToken, ct);
}
