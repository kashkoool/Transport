using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Infrastructure.Email;

/// <summary>
/// Sends mail over SMTP using the built-in <see cref="SmtpClient"/> (no extra dependency).
/// Works with Mailpit locally and any SMTP relay in production. Registered only when an SMTP
/// host is configured; otherwise the logging sink is used.
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(1, _options.SmtpTimeoutSeconds));

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.UseStartTls,
            Timeout = (int)timeout.TotalMilliseconds, // bounds the synchronous path
        };
        if (!string.IsNullOrEmpty(_options.SmtpUsername))
            client.Credentials = new NetworkCredential(_options.SmtpUsername, _options.SmtpPassword);

        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true,
        };
        mail.To.Add(message.ToEmail);

        // SmtpClient.Timeout does NOT reliably bound SendMailAsync, so cancel the awaited send
        // ourselves: a stalled relay surfaces as a cancellation rather than hanging the caller.
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        await client.SendMailAsync(mail, timeoutSource.Token);
    }
}
