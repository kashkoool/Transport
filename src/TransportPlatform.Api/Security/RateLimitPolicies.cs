namespace TransportPlatform.Api.Security;

/// <summary>
/// Named rate-limit policy keys. Endpoints opt into a stricter tier with
/// <c>.RequireRateLimiting(RateLimitPolicies.X)</c>; everything else inherits the global limiter.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Auth flows (login/register/refresh) — tight, to blunt brute force.</summary>
    public const string Auth = "auth";

    /// <summary>Sensitive writes (booking hold/create, payment checkout).</summary>
    public const string Sensitive = "sensitive";
}
