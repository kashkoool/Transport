using Microsoft.Extensions.Logging;
using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Infrastructure.Email;

/// <summary>
/// Development email sink: instead of sending, it logs that an email WOULD be sent (subject +
/// masked recipient only). It deliberately does NOT log the body, which carries the verify/reset
/// link (a token) — to view the actual link locally, point Email:SmtpHost at a dev SMTP viewer such
/// as Mailpit (:1025). Selected only when no SMTP host is configured; production always uses a real
/// SMTP sender (enforced by StartupGuards).
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        LogEmail(logger, message.Subject, EmailMask.Mask(message.ToEmail), null);
        return Task.CompletedTask;
    }

    private static readonly Action<ILogger, string, string, Exception?> LogEmail =
        LoggerMessage.Define<string, string>(
            LogLevel.Information, new EventId(1, "DevEmail"),
            "[DEV EMAIL] would send \"{Subject}\" to {To} (configure Email:SmtpHost / Mailpit to deliver + view the link)");
}
