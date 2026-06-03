using TransportPlatform.Domain.Fleet;

namespace TransportPlatform.Application.Fleet;

/// <summary>Read model for a company's driver.</summary>
public sealed record DriverDto(Guid Id, string FullName, string? Phone, string? LicenseNumber)
{
    public static DriverDto From(Driver d) => new(d.Id, d.FullName, d.Phone, d.LicenseNumber);
}
