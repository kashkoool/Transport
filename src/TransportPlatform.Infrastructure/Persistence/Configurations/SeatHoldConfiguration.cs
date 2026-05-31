using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Domain.Bookings;

namespace TransportPlatform.Infrastructure.Persistence.Configurations;

internal sealed class SeatHoldConfiguration : IEntityTypeConfiguration<SeatHold>
{
    public void Configure(EntityTypeBuilder<SeatHold> builder)
    {
        builder.ToTable("seat_hold");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.HeldBy).HasMaxLength(256).IsRequired();

        // THE concurrency lock: a (trip, seat) can be actively held only once.
        // A filtered unique index lets a seat be re-held after a prior hold is consumed.
        // The column name is quoted because EF/Npgsql creates it case-sensitively as
        // "Consumed"; an unquoted "consumed" folds to lowercase and fails to resolve.
        builder.HasIndex(h => new { h.TripId, h.SeatNumber })
            .IsUnique()
            .HasFilter("\"Consumed\" = false");

        builder.HasIndex(h => h.ExpiresAtUtc); // for the expiry sweeper
        builder.HasIndex(h => h.BookingId);
    }
}
