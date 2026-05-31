using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Application.Identity;

public sealed record RefreshCommand(string RefreshToken);

/// <summary>
/// Rotates a refresh token for a new access+refresh pair. Reuse detection and expiry are
/// enforced by <see cref="ITokenService"/>.
/// </summary>
public sealed class RefreshHandler(ITokenService tokens)
{
    public async Task<TokenResponse> HandleAsync(RefreshCommand command, CancellationToken ct)
    {
        var issued = await tokens.RefreshAsync(command.RefreshToken, ct);
        return TokenResponse.From(issued);
    }
}
