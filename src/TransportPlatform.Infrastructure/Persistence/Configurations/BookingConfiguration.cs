using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.Infrastructure.Persistence.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("booking", t =>
        {
            // DB-level invariants: money is never negative and Status/Currency are well-formed.
            t.HasCheckConstraint(
                "CK_booking_amounts_nonneg", "\"TotalAmount\" >= 0 AND \"DiscountAmount\" >= 0");
            t.HasCheckConstraint("CK_booking_currency_format", "\"Currency\" ~ '^[A-Z]{3}$'");
            t.HasCheckConstraint(
                "CK_booking_status_valid",
                "\"Status\" IN ('PendingPayment', 'Confirmed', 'Cancelled', 'Expired')");
        });
        builder.HasKey(b => b.Id);
        builder.Property(b => b.CustomerEmail).HasMaxLength(256).IsRequired();
        builder.Property(b => b.Reference).HasMaxLength(40).IsRequired();
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(b => b.TotalAmount).HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(b => b.Currency).HasMaxLength(3).IsRequired();
        builder.Property(b => b.DiscountAmount).HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(b => b.PromoCode).HasMaxLength(40);
        builder.Property(b => b.IdempotencyKey).HasMaxLength(100).IsRequired();

        builder.HasIndex(b => b.Reference).IsUnique();
        builder.HasIndex(b => b.IdempotencyKey).IsUnique();  // idempotent booking creation
        builder.HasIndex(b => b.CustomerEmail);
        // Report access paths. Composite indexes carry leftmost-prefix coverage, so we deliberately
        // do NOT keep single-column Status/CreatedBy/TripId indexes too (dead write-weight on this
        // hot table): (TripId, Status) covers confirmed-booking counts AND TripId-only lookups + the
        // trip FK; (CreatedBy, CreatedAtUtc) covers the per-operator employee report; CreatedAtUtc
        // alone stays for admin date-range scans.
        builder.HasIndex(b => new { b.TripId, b.Status });
        builder.HasIndex(b => b.CreatedAtUtc);
        builder.HasIndex(b => new { b.CreatedBy, b.CreatedAtUtc });

        // FK → trip, Restrict: a trip with bookings can't be hard-deleted.
        builder.HasOne<Trip>().WithMany()
            .HasForeignKey(b => b.TripId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.Passengers)
            .WithOne()
            .HasForeignKey(p => p.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.SeatAssignments)
            .WithOne()
            .HasForeignKey(a => a.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.Passengers).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(b => b.SeatAssignments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
