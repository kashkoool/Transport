using TransportPlatform.Domain.Identity;

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

    /// <summary>
    /// Resolve (or provision) the local account behind an external login (e.g. Google).
    /// Order: already-linked login → existing account by email (link it) → new passwordless account.
    /// Links to an existing account only when its email is proven (already confirmed, or the provider
    /// asserts the email is verified) — never silently linking to an unverified local account, which
    /// would enable account takeover. New external accounts default to the Customer role.
    /// </summary>
    Task<AuthenticatedUser> FindOrCreateExternalUserAsync(
        string provider, string providerKey, string email, string? fullName,
        bool providerEmailVerified, CancellationToken ct = default);

    /// <summary>Create a company staff account (role <c>Staff</c>) bound to the company.</summary>
    Task<Guid> RegisterStaffAsync(
        Guid companyId, string email, string password, string fullName, StaffType staffType, CancellationToken ct = default);

    /// <summary>Resolve a user's id by email (for addressing notifications). Null if unknown.</summary>
    Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>The user ids of a company's manager(s) — recipients of admin → company messages.</summary>
    Task<IReadOnlyList<Guid>> ListCompanyManagerIdsAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>List a company's staff (page slice), ordered by email.</summary>
    Task<IReadOnlyList<StaffMember>> ListStaffAsync(Guid companyId, int skip, int take, CancellationToken ct = default);

    /// <summary>Count a company's staff (for pagination totals).</summary>
    Task<int> CountStaffAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>
    /// Suspend (lock out) or reactivate a staff member — scoped to the given company so a manager
    /// can only ever touch their OWN staff. Returns false if no such staff member exists there.
    /// </summary>
    Task<bool> SetStaffSuspendedAsync(Guid companyId, Guid staffId, bool suspended, CancellationToken ct = default);
}

/// <summary>An authenticated principal: id, email, role names and (for vendor staff) company.</summary>
public sealed record AuthenticatedUser(Guid UserId, string Email, IReadOnlyList<string> Roles, Guid? CompanyId = null);

/// <summary>Read model for a company staff member.</summary>
public sealed record StaffMember(Guid Id, string Email, string FullName, string StaffType, bool Suspended);
