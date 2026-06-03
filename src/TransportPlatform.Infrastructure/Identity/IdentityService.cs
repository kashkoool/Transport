using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Identity;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.Infrastructure.Identity;

/// <summary>ASP.NET Core Identity-backed implementation of <see cref="IIdentityService"/>.</summary>
public sealed class IdentityService(
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    ApplicationDbContext db,
    IClock clock) : IIdentityService
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
        return new AuthenticatedUser(user.Id, user.Email!, [.. userRoles], user.CompanyId, user.EmailConfirmed);
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

    public async Task<AuthenticatedUser> FindOrCreateExternalUserAsync(
        string provider, string providerKey, string email, string? fullName,
        bool providerEmailVerified, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // 1) Already linked → just sign that user in.
        var linked = await users.FindByLoginAsync(provider, providerKey);
        if (linked is not null)
            return await ToAuthenticatedUserAsync(linked);

        // 2) An account already exists for this email → link the external login to it, but only
        //    when the email is proven. Refusing to link an UNVERIFIED local account stops an
        //    attacker who pre-registered a victim's email from hijacking it via OAuth.
        var byEmail = string.IsNullOrWhiteSpace(email) ? null : await users.FindByEmailAsync(email);
        if (byEmail is not null)
        {
            if (!byEmail.EmailConfirmed && !providerEmailVerified)
                throw new ConflictException("auth.link_requires_verification",
                    "An account with this email exists but isn't verified. Sign in with your password (or verify your email) first.");

            var addLink = await users.AddLoginAsync(byEmail, new UserLoginInfo(provider, providerKey, provider));
            if (!addLink.Succeeded)
                throw new ConflictException("auth.external_link_failed",
                    string.Join("; ", addLink.Errors.Select(e => e.Description)));

            if (!byEmail.EmailConfirmed && providerEmailVerified)
            {
                byEmail.EmailConfirmed = true; // the provider proved ownership of the address
                var update = await users.UpdateAsync(byEmail);
                if (!update.Succeeded)
                    throw new ConflictException("auth.external_link_failed",
                        string.Join("; ", update.Errors.Select(e => e.Description)));
            }
            return await ToAuthenticatedUserAsync(byEmail);
        }

        // 3) Brand-new, passwordless account (sign-in is only ever via the external provider).
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = providerEmailVerified,
        };
        var created = await users.CreateAsync(user);
        if (!created.Succeeded)
            throw new ConflictException("auth.external_registration_failed",
                string.Join("; ", created.Errors.Select(e => e.Description)));

        var addLogin = await users.AddLoginAsync(user, new UserLoginInfo(provider, providerKey, provider));
        if (!addLogin.Succeeded)
        {
            // The account would be passwordless AND unlinked → impossible to ever sign in. Remove it
            // rather than leave an orphan, then surface the failure.
            await users.DeleteAsync(user);
            throw new ConflictException("auth.external_link_failed",
                string.Join("; ", addLogin.Errors.Select(e => e.Description)));
        }

        await EnsureRoleAsync(UserRoles.Customer);
        await users.AddToRoleAsync(user, UserRoles.Customer);

        return new AuthenticatedUser(user.Id, email, [UserRoles.Customer]);
    }

    public async Task<Guid> RegisterStaffAsync(
        Guid companyId, string email, string password, string fullName, StaffType staffType, CancellationToken ct = default)
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
            StaffType = staffType,
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new ConflictException("auth.registration_failed",
                string.Join("; ", result.Errors.Select(e => e.Description)));

        await EnsureRoleAsync(UserRoles.Staff);
        await users.AddToRoleAsync(user, UserRoles.Staff);

        return user.Id;
    }

    public async Task<IReadOnlyList<StaffMember>> ListStaffAsync(Guid companyId, int skip, int take, string? search, CancellationToken ct = default)
    {
        var page = await StaffQuery(companyId, search)
            .OrderBy(u => u.Email)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        var now = clock.UtcNow;
        return page
            .Select(u => new StaffMember(
                u.Id, u.Email!, u.FullName ?? string.Empty, u.StaffType!.Value.ToString(),
                u.LockoutEnd is { } end && end > now))
            .ToList();
    }

    public Task<int> CountStaffAsync(Guid companyId, string? search, CancellationToken ct = default) =>
        StaffQuery(companyId, search).CountAsync(ct);

    // Staff are the company's users that carry a StaffType (the manager shares the company id but
    // has no StaffType, so this excludes them). Optional case-insensitive name/email contains-filter.
    private IQueryable<ApplicationUser> StaffQuery(Guid companyId, string? search)
    {
        var q = users.Users.Where(u => u.CompanyId == companyId && u.StaffType != null);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            // Server-side SQL lower()/LIKE inside this EF expression tree → analyzers are false positives.
#pragma warning disable CA1304, CA1311, CA1862
            q = q.Where(u => u.Email!.ToLower().Contains(term)
                || (u.FullName != null && u.FullName.ToLower().Contains(term)));
#pragma warning restore CA1304, CA1311, CA1862
        }
        return q;
    }

    public async Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var user = string.IsNullOrWhiteSpace(email) ? null : await users.FindByEmailAsync(email);
        return user?.Id;
    }

    public async Task<IReadOnlyList<Guid>> ListCompanyManagerIdsAsync(Guid companyId, CancellationToken ct = default)
    {
        // Managers share the company id but (unlike staff) carry no StaffType.
        return await users.Users
            .Where(u => u.CompanyId == companyId && u.StaffType == null)
            .Select(u => u.Id)
            .ToListAsync(ct);
    }

    public async Task DeleteCompanyUsersAsync(Guid companyId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var companyUsers = await users.Users.Where(u => u.CompanyId == companyId).ToListAsync(ct);
        foreach (var user in companyUsers)
            await users.DeleteAsync(user);
    }

    public async Task<bool> SetStaffSuspendedAsync(Guid companyId, Guid staffId, bool suspended, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var user = await users.FindByIdAsync(staffId.ToString());
        // Tenancy guard: only a staff member OF THIS company can be toggled.
        if (user is null || user.CompanyId != companyId || user.StaffType is null)
            return false;

        await users.SetLockoutEnabledAsync(user, true);
        var until = suspended ? DateTimeOffset.MaxValue : (DateTimeOffset?)null;
        var result = await users.SetLockoutEndDateAsync(user, until);
        return result.Succeeded;
    }

    public async Task<bool> UpdateStaffAsync(Guid companyId, Guid staffId, string fullName, StaffType staffType, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(staffId.ToString());
        if (user is null || user.CompanyId != companyId || user.StaffType is null)
            return false;

        user.FullName = fullName.Trim();
        user.StaffType = staffType;
        var result = await users.UpdateAsync(user);
        return result.Succeeded;
    }

    public async Task<bool> DeleteStaffAsync(Guid companyId, Guid staffId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(staffId.ToString());
        if (user is null || user.CompanyId != companyId || user.StaffType is null)
            return false;

        var result = await users.DeleteAsync(user);
        return result.Succeeded;
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
            return false;
        // ChangePasswordAsync verifies the current password; a wrong one fails (no enumeration).
        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        return result.Succeeded;
    }

    public async Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
            return null;
        var userRoles = await users.GetRolesAsync(user);
        return new UserProfile(user.Email!, user.FullName ?? string.Empty, user.PhoneNumber, [.. userRoles]);
    }

    public async Task<UserProfile?> UpdateProfileAsync(Guid userId, string fullName, string? phone, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
            return null;

        user.FullName = fullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
            throw new ConflictException("profile.update_failed",
                string.Join("; ", result.Errors.Select(e => e.Description)));

        var userRoles = await users.GetRolesAsync(user);
        return new UserProfile(user.Email!, user.FullName, user.PhoneNumber, [.. userRoles]);
    }

    public async Task<IReadOnlyList<CustomerAccount>> ListCustomersAsync(int skip, int take, string? search, CancellationToken ct = default)
    {
        var page = await (await CustomersQueryAsync(search))
            .OrderBy(u => u.Email)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        var now = clock.UtcNow;
        return page
            .Select(u => new CustomerAccount(
                u.Id, u.Email!, u.FullName ?? string.Empty, u.LockoutEnd is { } end && end > now))
            .ToList();
    }

    public async Task<int> CountCustomersAsync(string? search, CancellationToken ct = default) =>
        await (await CustomersQueryAsync(search)).CountAsync(ct);

    public async Task<CustomerAccount?> FindCustomerAsync(Guid userId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !await users.IsInRoleAsync(user, UserRoles.Customer))
            return null;
        var now = clock.UtcNow;
        return new CustomerAccount(user.Id, user.Email!, user.FullName ?? string.Empty, user.LockoutEnd is { } end && end > now);
    }

    public async Task<bool> SetCustomerSuspendedAsync(Guid userId, bool suspended, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !await users.IsInRoleAsync(user, UserRoles.Customer))
            return false;

        await users.SetLockoutEnabledAsync(user, true);
        var until = suspended ? DateTimeOffset.MaxValue : (DateTimeOffset?)null;
        var result = await users.SetLockoutEndDateAsync(user, until);
        return result.Succeeded;
    }

    public async Task<bool> DeleteCustomerAsync(Guid userId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !await users.IsInRoleAsync(user, UserRoles.Customer))
            return false;
        var result = await users.DeleteAsync(user);
        return result.Succeeded;
    }

    // Customers are the users in the Customer role (distinguishes them from admins, who also have
    // no company/staff-type). Optional case-insensitive name/email contains-filter.
    private async Task<IQueryable<ApplicationUser>> CustomersQueryAsync(string? search)
    {
        var customerRole = await roles.FindByNameAsync(UserRoles.Customer);
        if (customerRole is null)
            return users.Users.Where(_ => false); // no Customer role yet → no customers

        var roleId = customerRole.Id;
        var q = users.Users.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            // Server-side SQL lower()/LIKE inside this EF expression tree → analyzers are false positives.
#pragma warning disable CA1304, CA1311, CA1862
            q = q.Where(u => u.Email!.ToLower().Contains(term)
                || (u.FullName != null && u.FullName.ToLower().Contains(term)));
#pragma warning restore CA1304, CA1311, CA1862
        }
        return q;
    }

    private async Task<AuthenticatedUser> ToAuthenticatedUserAsync(ApplicationUser user)
    {
        var userRoles = await users.GetRolesAsync(user);
        return new AuthenticatedUser(user.Id, user.Email!, [.. userRoles], user.CompanyId);
    }

    private async Task EnsureRoleAsync(string role)
    {
        if (!await roles.RoleExistsAsync(role))
            await roles.CreateAsync(new IdentityRole<Guid>(role));
    }
}
