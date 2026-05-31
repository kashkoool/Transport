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
}

/// <summary>An authenticated principal: their id, email and role names.</summary>
public sealed record AuthenticatedUser(Guid UserId, string Email, IReadOnlyList<string> Roles);
