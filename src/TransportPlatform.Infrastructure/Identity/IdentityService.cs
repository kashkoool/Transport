using Microsoft.AspNetCore.Identity;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Identity;

namespace TransportPlatform.Infrastructure.Identity;

/// <summary>ASP.NET Core Identity-backed implementation of <see cref="IIdentityService"/>.</summary>
public sealed class IdentityService(
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles) : IIdentityService
{
    public async Task<AuthenticatedUser> RegisterCustomerAsync(
        string email, string password, string fullName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (await users.FindByEmailAsync(email) is not null)
            throw new ConflictException("auth.email_taken", "An account with this email already exists.");

        var user = new ApplicationUser { UserName = email, Email = email, FullName = fullName };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new ConflictException("auth.registration_failed",
                string.Join("; ", result.Errors.Select(e => e.Description)));

        await EnsureRoleAsync(UserRoles.Customer);
        await users.AddToRoleAsync(user, UserRoles.Customer);

        return new AuthenticatedUser(user.Id, email, [UserRoles.Customer]);
    }

    public async Task<AuthenticatedUser?> ValidateCredentialsAsync(
        string email, string password, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var user = await users.FindByEmailAsync(email);
        if (user is null)
            return null; // same outcome as a wrong password — no user enumeration

        if (await users.IsLockedOutAsync(user))
            throw new UnauthorizedException("auth.locked_out", "Account temporarily locked due to failed attempts.");

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user); // counts toward lockout
            return null;
        }

        await users.ResetAccessFailedCountAsync(user);
        var userRoles = await users.GetRolesAsync(user);
        return new AuthenticatedUser(user.Id, user.Email!, [.. userRoles], user.CompanyId);
    }

    public async Task<AuthenticatedUser> RegisterVendorManagerAsync(
        string email, string password, string fullName, Guid companyId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (await users.FindByEmailAsync(email) is not null)
            throw new ConflictException("auth.email_taken", "An account with this email already exists.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            CompanyId = companyId,
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new ConflictException("auth.registration_failed",
                string.Join("; ", result.Errors.Select(e => e.Description)));

        await EnsureRoleAsync(UserRoles.VendorManager);
        await users.AddToRoleAsync(user, UserRoles.VendorManager);

        return new AuthenticatedUser(user.Id, email, [UserRoles.VendorManager], companyId);
    }

    public async Task<string> GenerateEmailConfirmationTokenAsync(Guid userId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId.ToString());
        return user is null ? string.Empty : await users.GenerateEmailConfirmationTokenAsync(user);
    }

    public async Task<bool> ConfirmEmailAsync(string email, string token, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByEmailAsync(email);
        if (user is null)
            return false;
        var result = await users.ConfirmEmailAsync(user, token);
        return result.Succeeded;
    }

    public async Task<(Guid UserId, string Token)?> CreateEmailVerificationTokenAsync(string email, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByEmailAsync(email);
        if (user is null || user.EmailConfirmed)
            return null;
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        return (user.Id, token);
    }

    public async Task<(Guid UserId, string Token)?> CreatePasswordResetTokenAsync(string email, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByEmailAsync(email);
        if (user is null)
            return null;
        var token = await users.GeneratePasswordResetTokenAsync(user);
        return (user.Id, token);
    }

    public async Task<Guid?> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByEmailAsync(email);
        if (user is null)
            return null;
        var result = await users.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded ? user.Id : null;
    }

    private async Task EnsureRoleAsync(string role)
    {
        if (!await roles.RoleExistsAsync(role))
            await roles.CreateAsync(new IdentityRole<Guid>(role));
    }
}
