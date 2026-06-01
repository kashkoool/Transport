namespace TransportPlatform.Application.Common;

/// <summary>
/// The authenticated caller, resolved from the request's claims. Lets use-case handlers
/// enforce tenant isolation (company scoping) without touching HTTP or ASP.NET types.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    /// <summary>The vendor company the caller belongs to (vendor managers/staff); null otherwise.</summary>
    Guid? CompanyId { get; }

    bool IsAuthenticated { get; }

    /// <summary>The caller's user id, or throws if unauthenticated.</summary>
    Guid RequireUserId();

    /// <summary>
    /// The caller's company id, or throws if absent. Used by vendor-scoped handlers so a
    /// company manager can only ever act within their own tenant.
    /// </summary>
    Guid RequireCompanyId();
}
