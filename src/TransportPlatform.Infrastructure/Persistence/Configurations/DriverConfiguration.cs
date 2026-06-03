using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Fleet;

namespace TransportPlatform.Infrastructure.Persistence.Configurations;

internal sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("driver");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.FullName).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Phone).HasMaxLength(40);
        builder.Property(d => d.LicenseNumber).HasMaxLength(60);
        builder.HasIndex(d => d.CompanyId);

        // FK → company (no navigation). Restrict: a company with drivers can't be hard-deleted.
        builder.HasOne<Company>().WithMany()
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
