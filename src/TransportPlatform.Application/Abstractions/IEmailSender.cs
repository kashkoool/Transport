namespace TransportPlatform.Application.Abstractions;

/// <summary>A transactional email to send (HTML body; the platform sends no marketing mail).</summary>
public sealed record EmailMessage(string ToEmail, string Subject, string HtmlBody);

/// <summary>
/// Port for sending transactional email (verification, password reset, booking confirmation).
/// Implemented by an SMTP sender in production and a logging sink in development, so the app
/// layer never depends on a concrete mail transport.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
