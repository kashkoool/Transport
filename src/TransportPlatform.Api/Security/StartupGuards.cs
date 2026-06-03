namespace TransportPlatform.Api.Security;

/// <summary>
/// Fail-fast configuration checks run at startup. In production a misconfigured secret should
/// stop the app from booting, never silently run insecure.
/// </summary>
public static class StartupGuards
{
    private const int MinSigningKeyLength = 32;

    // Placeholder fragments shipped in appsettings/.env.example that must never reach production.
    private static readonly string[] KnownPlaceholders =
    [
        "change_me",
        "dev_only",
        "dev_sandbox",
        "integration-tests",
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

        var allowedHosts = config["AllowedHosts"] ?? string.Empty;
        if (allowedHosts.Trim() is "" or "*")
            problems.Add("AllowedHosts must be an explicit host allow-list in production (not '*'), to prevent host-header injection.");

        // Email: production must send real mail (the dev logging sink is for local only) and
        // build links to the real web app.
        if (string.IsNullOrWhiteSpace(config["Email:SmtpHost"]))
            problems.Add("Email:SmtpHost must be set in production (the logging sink is dev-only).");
        if (string.IsNullOrWhiteSpace(config["Email:FromAddress"]))
            problems.Add("Email:FromAddress must be set in production.");
        var frontendUrl = config["Email:FrontendBaseUrl"] ?? string.Empty;
        if (!Uri.IsWellFormedUriString(frontendUrl, UriKind.Absolute))
            problems.Add("Email:FrontendBaseUrl must be a valid absolute URL in production (used to build email links).");

        // Google sign-in is optional, but if a client id is configured it must be a real one with
        // a secret (a placeholder client id would silently break the OAuth handshake in production).
        var googleClientId = config["OAuth:Google:ClientId"] ?? string.Empty;
        if (googleClientId.Length > 0)
        {
            if (KnownPlaceholders.Any(p => googleClientId.Contains(p, StringComparison.OrdinalIgnoreCase)))
                problems.Add("OAuth:Google:ClientId is still a known placeholder value.");

            var googleClientSecret = config["OAuth:Google:ClientSecret"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(googleClientSecret))
                problems.Add("OAuth:Google:ClientSecret must be set when Google sign-in is enabled.");
            else if (KnownPlaceholders.Any(p => googleClientSecret.Contains(p, StringComparison.OrdinalIgnoreCase)))
                problems.Add("OAuth:Google:ClientSecret is still a known placeholder value.");
        }

        if (problems.Count > 0)
            throw new InvalidOperationException(
                "Insecure production configuration:" + Environment.NewLine +
                string.Join(Environment.NewLine, problems.Select(p => "  - " + p)));
    }
}
