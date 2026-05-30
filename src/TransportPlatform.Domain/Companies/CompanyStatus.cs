namespace TransportPlatform.Domain.Companies;

/// <summary>Lifecycle of a vendor (transport company) on the platform.</summary>
public enum CompanyStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
}
