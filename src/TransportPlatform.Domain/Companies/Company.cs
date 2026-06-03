using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Companies;

/// <summary>
/// A vendor: a transport company that owns buses and runs trips. The tenant boundary —
/// staff and managers only ever see data scoped to their <see cref="Company"/>.
/// </summary>
public sealed class Company : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string? Phone { get; private set; }
    public CompanyStatus Status { get; private set; } = CompanyStatus.Pending;

    private Company() { } // EF

    public Company(string name, string email, string? phone)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("company.name_required", "Company name is required.");
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("company.email_required", "Company email is required.");

        Name = name.Trim();
        Email = email.Trim().ToLowerInvariant();
        Phone = phone?.Trim();
    }

    /// <summary>Edit the company profile (name + phone). Email is immutable (it's the unique key).</summary>
    public void Update(string name, string? phone)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("company.name_required", "Company name is required.");
        Name = name.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
    }

    public void Activate() => Status = CompanyStatus.Active;

    public void Suspend() => Status = CompanyStatus.Suspended;

    public bool CanOperate => Status == CompanyStatus.Active;
}
