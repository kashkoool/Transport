using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Fleet;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.Infrastructure.Persistence.Configurations;

internal sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("trip");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Origin).HasMaxLength(120).IsRequired();
        builder.Property(t => t.Destination).HasMaxLength(120).IsRequired();
        builder.Property(t => t.Price).HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(t => t.Currency).HasMaxLength(3).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Index supports the route+date search query.
        builder.HasIndex(t => new { t.Origin, t.Destination, t.DepartureUtc });
        builder.HasIndex(t => t.CompanyId);

        // FKs (no navigations). Restrict on both: a company/bus with scheduled trips can't be
        // hard-deleted, preventing orphaned trips.
        builder.HasOne<Company>().WithMany()
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bus>().WithMany()
            .HasForeignKey(t => t.BusId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
