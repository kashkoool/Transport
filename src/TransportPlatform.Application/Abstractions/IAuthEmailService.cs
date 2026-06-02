namespace TransportPlatform.Application.Abstractions;

/// <summary>
/// Sends the auth-related emails (verification, password reset). The application layer calls
/// these with just the email + token; Infrastructure owns the link-building (frontend URL) and
/// templates. Implementations are best-effort — they log and swallow transport failures so an
/// email problem never fails registration or leaks whether an account exists.
/// </summary>
public interface IAuthEmailService
{
    Task SendEmailVerificationAsync(string email, string token, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken = default);
}
