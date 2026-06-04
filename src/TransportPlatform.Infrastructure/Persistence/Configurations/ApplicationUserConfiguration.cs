using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Infrastructure.Identity;

namespace TransportPlatform.Infrastructure.Persistence.Configurations;

/// <summary>
/// Adds the custom <see cref="ApplicationUser"/> columns on top of the Identity defaults
/// (applied after <c>base.OnModelCreating</c>). StaffType is stored as its string name.
/// </summary>
internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.StaffType).HasConversion<string>().HasMaxLength(20);
        // Vendor staff/manager lookups and company-delete all filter users by company.
        builder.HasIndex(u => u.CompanyId);
    }
}
