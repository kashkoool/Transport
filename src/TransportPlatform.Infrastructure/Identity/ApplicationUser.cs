using Microsoft.AspNetCore.Identity;
using TransportPlatform.Domain.Identity;

namespace TransportPlatform.Infrastructure.Identity;

/// <summary>
/// Application user backed by ASP.NET Core Identity. Kept in Infrastructure so the auth
/// provider can later be swapped for Keycloak without touching the domain.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }

    /// <summary>Set for vendor staff/managers; null for customers and platform admins.</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>
    /// Set for company staff accounts (role <c>Staff</c>); null for managers, customers and admins.
    /// Lets staff be listed/managed separately from the company manager who shares the company id.
    /// </summary>
    public StaffType? StaffType { get; set; }

    /// <summary>
    /// The recipient's preferred language for transactional email (<c>"en"</c> or <c>"ar"</c>).
    /// Null means unknown — callers treat that as English.
    /// </summary>
    public string? Language { get; set; }
}
