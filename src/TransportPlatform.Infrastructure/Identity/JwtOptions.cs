namespace TransportPlatform.Infrastructure.Identity;

/// <summary>Strongly-typed JWT settings, bound from the "Jwt" configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "transport-platform";
    public string Audience { get; set; } = "transport-platform-clients";

    /// <summary>HMAC-SHA256 signing key. Must be >= 32 bytes. Supplied via secrets/env in real envs.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
