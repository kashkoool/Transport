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

    /// <summary>
    /// Payment gateway webhook — anonymous but signature-verified. Generous so legitimate
    /// gateway retries pass, but bounded so a flood of forged payloads can't exhaust the box.
    /// </summary>
    public const string Webhook = "webhook";

    /// <summary>
    /// Public, anonymous reads (trip search, seat-map, stops, company directory, public reviews).
    /// A dedicated per-IP tier so the most DoS-exposed endpoints aren't bounded only by the shared
    /// global limiter.
    /// </summary>
    public const string PublicRead = "public-read";

    /// <summary>
    /// Report exports (CSV/XLSX/PDF) — compute/memory-heavy document generation. Tight, since each
    /// call costs far more than a JSON read.
    /// </summary>
    public const string Export = "export";
}
