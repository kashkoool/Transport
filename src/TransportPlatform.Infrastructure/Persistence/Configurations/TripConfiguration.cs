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
        builder.ToTable("trip", t =>
        {
            // DB-level invariants (defence in depth beyond the domain guards): money is never
            // negative, currency is a 3-letter uppercase code, and Status is a known value.
            t.HasCheckConstraint("CK_trip_price_nonneg", "\"Price\" >= 0");
            t.HasCheckConstraint("CK_trip_currency_format", "\"Currency\" ~ '^[A-Z]{3}$'");
            t.HasCheckConstraint(
                "CK_trip_status_valid",
                "\"Status\" IN ('Scheduled', 'InProgress', 'Completed', 'Cancelled')");
        });
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Origin).HasMaxLength(120).IsRequired();
        builder.Property(t => t.Destination).HasMaxLength(120).IsRequired();
        builder.Property(t => t.Price).HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(t => t.Currency).HasMaxLength(3).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Route+date search index is created in the migration as raw SQL because it must be a
        // FUNCTIONAL, PARTIAL index that EF's fluent API can't express: the query compares
        // lower(origin)/lower(destination) and only ever wants Scheduled trips, so the index is
        //   (lower("Origin"), lower("Destination"), "DepartureUtc") WHERE "Status" = 'Scheduled'.
        // A plain (Origin,Destination,DepartureUtc) index would NOT be used for the lower() compare.
        //
        // (CompanyId, DepartureUtc) serves the per-company report date-range scans AND the vendor
        // trip list (filter CompanyId, order by DepartureUtc); CompanyId-only lookups + the FK use
        // its leftmost prefix, so no separate single-column CompanyId index is needed.
        builder.HasIndex(t => new { t.CompanyId, t.DepartureUtc });

        // Waypoints are owned by the trip: loaded via the backing field, cascade-deleted with it.
        builder.HasMany(t => t.Stops)
            .WithOne()
            .HasForeignKey(s => s.TripId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(Trip.Stops))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

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
