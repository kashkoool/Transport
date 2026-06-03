using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Fleet;

/// <summary>
/// A bus driver employed by a company. Drivers do NOT log in (no account); they are reference
/// records that can be assigned to a bus.
/// </summary>
public sealed class Driver : AggregateRoot
{
    public Guid CompanyId { get; private set; }
    public string FullName { get; private set; } = null!;
    public string? Phone { get; private set; }
    public string? LicenseNumber { get; private set; }

    private Driver() { } // EF

    public Driver(Guid companyId, string fullName, string? phone = null, string? licenseNumber = null)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("driver.company_required", "A driver must belong to a company.");
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("driver.name_required", "Driver name is required.");

        CompanyId = companyId;
        FullName = fullName.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        LicenseNumber = string.IsNullOrWhiteSpace(licenseNumber) ? null : licenseNumber.Trim();
    }
}
