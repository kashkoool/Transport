namespace TransportPlatform.Application.Common;

/// <summary>
/// Names of the authorization policies. Registered in Infrastructure (mapping each to the
/// concrete roles) and applied on endpoints with <c>.RequireAuthorization(name)</c>.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Platform staff: Admin or SuperAdmin.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>A vendor company manager (acts only within their own company).</summary>
    public const string VendorOnly = "VendorOnly";

    /// <summary>A vendor manager OR their staff (both scoped to the same company).</summary>
    public const string VendorOrStaff = "VendorOrStaff";
}
