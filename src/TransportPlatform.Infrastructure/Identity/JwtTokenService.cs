using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Identity;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.Infrastructure.Identity;

/// <summary>
/// First-party token service: short-lived HS256 JWT access tokens + long-lived, single-use,
/// DB-backed refresh tokens with rotation and reuse detection. Refresh tokens are stored only
/// as SHA-256 hashes, never in the clear.
/// </summary>
public sealed class JwtTokenService(
    IOptions<JwtOptions> options,
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    IClock clock) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public async Task<AuthTokens> IssueAsync(Guid userId, string email, IReadOnlyList<string> roles, Guid? companyId, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var (raw, hash) = GenerateRefreshToken();
        var refreshExpires = now.AddDays(_options.RefreshTokenDays);

        db.RefreshTokens.Add(new RefreshToken(userId, hash, now, refreshExpires));
        await db.SaveChangesAsync(ct);

        var access = CreateAccessToken(userId, email, roles, companyId, now);
        return new AuthTokens(access, now.AddMinutes(_options.AccessTokenMinutes), raw, refreshExpires);
    }

    public async Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var hash = HashToken(refreshToken);

        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
            ?? throw new UnauthorizedException("auth.invalid_refresh", "Invalid refresh token.");

        // A consumed token presented again means it was rotated or revoked already — likely
        // stolen. Revoke the entire family for that user as a safety response.
        if (token.IsConsumed)
        {
            await RevokeAllForUserAsync(token.UserId, now, ct);
            throw new UnauthorizedException("auth.refresh_reuse", "Refresh token reuse detected; session revoked.");
        }

        if (!token.IsActive(now))
            throw new UnauthorizedException("auth.refresh_expired", "Refresh token has expired.");

        var user = await users.FindByIdAsync(token.UserId.ToString())
            ?? throw new UnauthorizedException("auth.user_missing", "User no longer exists.");
        string[] roles = [.. await users.GetRolesAsync(user)];

        var (raw, newHash) = GenerateRefreshToken();
        var refreshExpires = now.AddDays(_options.RefreshTokenDays);
        token.Rotate(newHash, now);
        db.RefreshTokens.Add(new RefreshToken(user.Id, newHash, now, refreshExpires));
        await db.SaveChangesAsync(ct);

        var access = CreateAccessToken(user.Id, user.Email!, roles, user.CompanyId, now);
        return new AuthTokens(access, now.AddMinutes(_options.AccessTokenMinutes), raw, refreshExpires);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = HashToken(refreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is not null && !token.IsConsumed)
        {
            token.Revoke(clock.UtcNow);
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var t in active)
            t.Revoke(now);
        await db.SaveChangesAsync(ct);
    }

    private string CreateAccessToken(Guid userId, string email, IReadOnlyList<string> roles, Guid? companyId, DateTimeOffset now)
    {
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new("email", email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (companyId is { } cid)
            claims.Add(new Claim("company_id", cid.ToString()));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(_options.AccessTokenMinutes).UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static (string Raw, string Hash) GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var raw = Base64UrlEncode(bytes);
        return (raw, HashToken(raw));
    }

    private static string HashToken(string raw) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
