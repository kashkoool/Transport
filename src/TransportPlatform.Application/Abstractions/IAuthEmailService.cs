namespace TransportPlatform.Application.Abstractions;

/// <summary>
/// Sends the auth-related emails (verification, password reset). The application layer calls
/// these with just the email + token; Infrastructure owns the link-building (frontend URL) and
/// templates. Implementations are best-effort — they log and swallow transport failures so an
/// email problem never fails registration or leaks whether an account exists.
/// </summary>
public interface IAuthEmailService
{
    /// <summary>Sends the email-address verification link to a newly registered user.</summary>
    /// <param name="lang">Recipient's preferred language (<c>"en"</c>/<c>"ar"</c>); null → English.</param>
    Task SendEmailVerificationAsync(string email, string token, string? lang = null, CancellationToken cancellationToken = default);

    /// <summary>Sends the password-reset link to a user who requested one.</summary>
    /// <param name="lang">Recipient's preferred language (<c>"en"</c>/<c>"ar"</c>); null → English.</param>
    Task SendPasswordResetAsync(string email, string token, string? lang = null, CancellationToken cancellationToken = default);
}
