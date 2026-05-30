using Microsoft.AspNetCore.Identity;

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
}
