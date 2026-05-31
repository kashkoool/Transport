using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Identity;

/// <summary>
/// A single-use refresh token, stored as a SHA-256 hash (never the raw value). Rotation:
/// using a token mints a new one and marks this one consumed via <see cref="ReplacedByHash"/>.
/// If a consumed token is presented again (reuse detection), the whole family is revoked.
/// </summary>
public sealed class RefreshToken : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? ReplacedByHash { get; private set; }

    private RefreshToken() { } // EF

    public RefreshToken(Guid userId, string tokenHash, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
    {
        if (userId == Guid.Empty)
            throw new DomainException("refresh.user_required", "A refresh token must belong to a user.");
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("refresh.hash_required", "A refresh token hash is required.");

        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public bool IsActive(DateTimeOffset nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;

    /// <summary>True once the token has been rotated or explicitly revoked.</summary>
    public bool IsConsumed => RevokedAtUtc is not null;

    /// <summary>Rotate: mark this token consumed and point it at its successor.</summary>
    public void Rotate(string replacedByHash, DateTimeOffset nowUtc)
    {
        RevokedAtUtc = nowUtc;
        ReplacedByHash = replacedByHash;
    }

    public void Revoke(DateTimeOffset nowUtc) => RevokedAtUtc ??= nowUtc;
}
