using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Application.Identity;

/// <summary>The verified identity Google handed us after a successful OAuth handshake.</summary>
public sealed record GoogleSignInCommand(string ProviderKey, string Email, string? FullName, bool EmailVerified);

/// <summary>
/// Completes a Google sign-in/sign-up: resolves or provisions the local account for the external
/// login, then issues the same token pair as a password login (so the rest of the app is unaware
/// of how the user authenticated).
/// </summary>
public sealed class GoogleSignInHandler(IIdentityService identity, ITokenService tokens)
{
    public const string Provider = "Google";

    public async Task<AuthResult> HandleAsync(GoogleSignInCommand command, CancellationToken ct)
    {
        var user = await identity.FindOrCreateExternalUserAsync(
            Provider, command.ProviderKey, command.Email, command.FullName, command.EmailVerified, ct);
        var issued = await tokens.IssueAsync(user.UserId, user.Email, user.Roles, user.CompanyId, ct);
        return AuthResult.From(issued, user);
    }
}
