namespace TransportPlatform.Application.Abstractions;

/// <summary>
/// User registration + credential checks. Abstracts ASP.NET Core Identity so the auth
/// use-cases stay free of Infrastructure and can be re-pointed at Keycloak later.
/// </summary>
public interface IIdentityService
{
    /// <summary>Create a new Customer account. Throws ConflictException if the email is taken.</summary>
    Task<AuthenticatedUser> RegisterCustomerAsync(string email, string password, string fullName, CancellationToken ct = default);

    /// <summary>
    /// Verify an email/password pair. Returns the user (with roles) on success, null on a
    /// wrong password (deliberately indistinguishable from unknown email), or throws
    /// UnauthorizedException if the account is locked out.
    /// </summary>
    Task<AuthenticatedUser?> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);

    /// <summary>Create a vendor-manager account bound to a company (admin-invoked).</summary>
    Task<AuthenticatedUser> RegisterVendorManagerAsync(
        string email, string password, string fullName, Guid companyId, CancellationToken ct = default);

    /// <summary>Generate an email-confirmation token for a known user (used right after register).</summary>
    Task<string> GenerateEmailConfirmationTokenAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Confirm an email with a token. Returns false on unknown user or invalid token.</summary>
    Task<bool> ConfirmEmailAsync(string email, string token, CancellationToken ct = default);

    /// <summary>
    /// Issue a verification token by email (for "resend"). Null if the user doesn't exist or is
    /// already confirmed — callers must return a generic response either way (anti-enumeration).
    /// </summary>
    Task<(Guid UserId, string Token)?> CreateEmailVerificationTokenAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Issue a password-reset token by email. Null if no such user — callers must return a
    /// generic response either way (anti-enumeration).
    /// </summary>
    Task<(Guid UserId, string Token)?> CreatePasswordResetTokenAsync(string email, CancellationToken ct = default);

    /// <summary>Apply a password reset. Returns the user id on success, null on failure.</summary>
    Task<Guid?> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default);
}

/// <summary>An authenticated principal: id, email, role names and (for vendor staff) company.</summary>
public sealed record AuthenticatedUser(Guid UserId, string Email, IReadOnlyList<string> Roles, Guid? CompanyId = null);
