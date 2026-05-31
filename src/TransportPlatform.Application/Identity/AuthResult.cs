using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Application.Identity;

/// <summary>Full auth response after register/login: tokens + who the user is.</summary>
public sealed record AuthResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    string Email,
    IReadOnlyList<string> Roles)
{
    public static AuthResult From(AuthTokens tokens, AuthenticatedUser user) =>
        new(tokens.AccessToken, tokens.AccessTokenExpiresAtUtc,
            tokens.RefreshToken, tokens.RefreshTokenExpiresAtUtc, user.Email, user.Roles);
}

/// <summary>Lighter response for refresh: just the rotated token pair.</summary>
public sealed record TokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc)
{
    public static TokenResponse From(AuthTokens tokens) =>
        new(tokens.AccessToken, tokens.AccessTokenExpiresAtUtc,
            tokens.RefreshToken, tokens.RefreshTokenExpiresAtUtc);
}
