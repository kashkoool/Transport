using System.Security.Claims;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Api.Identity;

/// <summary>
/// Resolves <see cref="ICurrentUser"/> from the request's JWT claims (sub + company_id).
/// Scoped — one per request.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue("sub"), out var id) ? id : null;

    public string? Email => Principal?.FindFirstValue("email");

    public Guid? CompanyId =>
        Guid.TryParse(Principal?.FindFirstValue("company_id"), out var id) ? id : null;

    public Guid RequireUserId() =>
        UserId ?? throw new UnauthorizedException("auth.required", "Authentication is required.");

    public string RequireEmail() =>
        Email is { Length: > 0 } e ? e
            : throw new UnauthorizedException("auth.required", "Authentication is required.");

    public Guid RequireCompanyId() =>
        CompanyId ?? throw new UnauthorizedException("auth.company_required",
            "This action requires a vendor company account.");
}
