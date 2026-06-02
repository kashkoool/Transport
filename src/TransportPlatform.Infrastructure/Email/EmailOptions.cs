namespace TransportPlatform.Infrastructure.Email;

/// <summary>
/// Email transport + link-building config. When <see cref="SmtpHost"/> is empty the app uses a
/// logging sink (dev), otherwise SMTP. The Build* helpers produce the absolute links that go in
/// emails, pointing at the web app's routes.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = string.Empty; // empty → logging sink (dev default)
    public int SmtpPort { get; set; } = 1025; // Mailpit default
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public bool UseStartTls { get; set; } // false for local Mailpit; true for most real providers

    /// <summary>
    /// Hard bound on a single SMTP send. <c>SmtpClient.Timeout</c> only governs the synchronous
    /// path, so the sender also cancels the awaited send via a linked token after this window —
    /// a stalled relay can never hang the request thread or the outbox drain.
    /// </summary>
    public int SmtpTimeoutSeconds { get; set; } = 30;

    public string FromAddress { get; set; } = "noreply@tpx.local";
    public string FromName { get; set; } = "TPX Travel";

    /// <summary>Web app origin used to build links in emails (compose serves it on :8080).</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:8080";
    public string PasswordResetPath { get; set; } = "/reset-password";
    public string VerifyEmailPath { get; set; } = "/verify-email";

    public string BuildResetUrl(string email, string token) =>
        $"{TrimmedBase()}{PasswordResetPath}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

    public string BuildVerifyUrl(string email, string token) =>
        $"{TrimmedBase()}{VerifyEmailPath}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

    public string BuildTicketUrl(Guid bookingId) => $"{TrimmedBase()}/ticket/{bookingId}";

    private string TrimmedBase() => FrontendBaseUrl.TrimEnd('/');
}
