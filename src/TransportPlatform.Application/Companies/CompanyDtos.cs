using TransportPlatform.Domain.Companies;

namespace TransportPlatform.Application.Companies;

/// <summary>Read model for a vendor company.</summary>
public sealed record CompanyDto(Guid Id, string Name, string Email, string? Phone, string Status)
{
    public static CompanyDto From(Company c) =>
        new(c.Id, c.Name, c.Email, c.Phone, c.Status.ToString());
}

/// <summary>Result of creating a vendor-manager login bound to a company.</summary>
public sealed record CompanyManagerDto(Guid UserId, string Email, Guid CompanyId);
