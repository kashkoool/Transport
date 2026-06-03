using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Fleet;

namespace TransportPlatform.Infrastructure.Persistence.Configurations;

internal sealed class BusConfiguration : IEntityTypeConfiguration<Bus>
{
    public void Configure(EntityTypeBuilder<Bus> builder)
    {
        builder.ToTable("bus");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.BusNumber).HasMaxLength(40).IsRequired();
        builder.Property(b => b.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(b => b.Model).HasMaxLength(100);
        builder.HasIndex(b => new { b.CompanyId, b.BusNumber }).IsUnique();

        // FK → company (no navigation on the domain entity). Restrict: a company with buses
        // can't be hard-deleted out from under them.
        builder.HasOne<Company>().WithMany()
            .HasForeignKey(b => b.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional assigned driver. SetNull so deleting a driver simply unassigns their buses.
        builder.HasOne<Driver>().WithMany()
            .HasForeignKey(b => b.DriverId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
