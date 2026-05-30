namespace TransportPlatform.Domain.Identity;

/// <summary>
/// The fixed set of roles in the platform. Mirrors the actor model in SYSTEM_MAP.md.
/// Kept as constants (not an enum) because ASP.NET Core Identity works with role names.
/// </summary>
public static class UserRoles
{
    public const string Customer = "Customer";
    public const string Staff = "Staff";
    public const string VendorManager = "VendorManager";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";

    public static readonly IReadOnlyList<string> All =
        [Customer, Staff, VendorManager, Admin, SuperAdmin];
}
