namespace TransportPlatform.Api.Security;

/// <summary>
/// Fail-fast configuration checks run at startup. In production a misconfigured secret should
/// stop the app from booting, never silently run insecure.
/// </summary>
public static class StartupGuards
{
    private const int MinSigningKeyLength = 32;

    // Placeholder values shipped in appsettings/.env.example that must never reach production.
    private static readonly string[] KnownPlaceholders =
    [
        "change_me",
        "change_me_to_a_long_random_secret_at_least_32_chars",
    ];

    public static void ValidateConfiguration(IConfiguration config, IHostEnvironment env)
    {
        if (!env.IsProduction())
            return;

        var problems = new List<string>();

        var signingKey = config["Jwt:SigningKey"] ?? string.Empty;
        if (signingKey.Length < MinSigningKeyLength)
            problems.Add($"Jwt:SigningKey must be at least {MinSigningKeyLength} characters.");
        if (KnownPlaceholders.Any(p => signingKey.Contains(p, StringComparison.OrdinalIgnoreCase)))
            problems.Add("Jwt:SigningKey is still a known placeholder value.");

        var connection = config.GetConnectionString("Postgres") ?? string.Empty;
        if (connection.Contains("change_me", StringComparison.OrdinalIgnoreCase))
            problems.Add("ConnectionStrings:Postgres still contains a placeholder password.");
        if (connection.Length > 0 && !connection.Contains("SSL Mode", StringComparison.OrdinalIgnoreCase))
            problems.Add("ConnectionStrings:Postgres should enable TLS (e.g. 'SSL Mode=Require') in production.");

        var webhookSecret = config["Payments:WebhookSecret"] ?? string.Empty;
        if (KnownPlaceholders.Any(p => webhookSecret.Contains(p, StringComparison.OrdinalIgnoreCase)))
            problems.Add("Payments:WebhookSecret is still a known placeholder value.");

        if (config.GetValue<int?>("Proxy:TrustedHops") is null or 0)
            problems.Add("Proxy:TrustedHops must be a positive integer in production so rate limits key on the real client IP.");

        if (problems.Count > 0)
            throw new InvalidOperationException(
                "Insecure production configuration:" + Environment.NewLine +
                string.Join(Environment.NewLine, problems.Select(p => "  - " + p)));
    }
}
