using Microsoft.Extensions.Logging;
using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Infrastructure.Email;

/// <summary>
/// Development email sink: instead of sending, it logs the message (including the action link)
/// so you can copy the verify/reset link from the API logs with zero SMTP setup. Selected only
/// when no SMTP host is configured — production always uses a real SMTP sender.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        LogEmail(logger, message.ToEmail, message.Subject, message.HtmlBody, null);
        return Task.CompletedTask;
    }

    private static readonly Action<ILogger, string, string, string, Exception?> LogEmail =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information, new EventId(1, "DevEmail"),
            "[DEV EMAIL] to {To} — {Subject}\n{Body}");
}
