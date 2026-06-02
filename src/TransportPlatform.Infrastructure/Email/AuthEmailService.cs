using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Infrastructure.Email;

/// <summary>
/// Composes auth emails (link + template) and sends them via <see cref="IEmailSender"/>.
/// Best-effort: a transport failure is logged (error class only — never the token/link) and
/// swallowed, so registration succeeds and forgot-password stays anti-enumeration-safe.
/// </summary>
public sealed class AuthEmailService(
    IEmailSender sender,
    IOptions<EmailOptions> options,
    ILogger<AuthEmailService> logger) : IAuthEmailService
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendEmailVerificationAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = _options.BuildVerifyUrl(email, token);
            await sender.SendAsync(EmailTemplates.Verification(email, url), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSendFailed(logger, "verification", ex);
        }
    }

    public async Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = _options.BuildResetUrl(email, token);
            await sender.SendAsync(EmailTemplates.PasswordReset(email, url), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSendFailed(logger, "password-reset", ex);
        }
    }

    private static readonly Action<ILogger, string, Exception?> LogSendFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, nameof(LogSendFailed)),
            "Auth email ({Kind}) failed to send.");
}
