namespace TransportPlatform.Application.Abstractions;

/// <summary>
/// Issues and validates auth tokens. Abstracted so the implementation (currently
/// first-party JWT + DB-backed refresh tokens) can later be swapped for Keycloak/OIDC
/// without touching the auth use-cases.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Mint a short-lived JWT access token plus a long-lived, single-use refresh token
    /// for the given user. The refresh token's raw value is returned to the caller once;
    /// only its hash is persisted.
    /// </summary>
    Task<AuthTokens> IssueAsync(Guid userId, string email, IReadOnlyList<string> roles, Guid? companyId, CancellationToken ct = default);

    /// <summary>
    /// Exchange a valid refresh token for a fresh access+refresh pair (rotation). Throws if
    /// the token is unknown, expired, or already used. Reuse of a consumed token revokes the
    /// whole token family (reuse detection).
    /// </summary>
    Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revoke a refresh token (logout). No-op if it does not exist.</summary>
    Task RevokeAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revoke ALL active refresh tokens for a user — e.g. after a password reset.</summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>An issued access token plus its companion refresh token.</summary>
public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);
