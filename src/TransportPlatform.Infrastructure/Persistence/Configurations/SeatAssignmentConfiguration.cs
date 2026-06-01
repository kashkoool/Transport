using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.Infrastructure.Persistence.Configurations;

internal sealed class SeatAssignmentConfiguration : IEntityTypeConfiguration<SeatAssignment>
{
    public void Configure(EntityTypeBuilder<SeatAssignment> builder)
    {
        builder.ToTable("seat_assignment");
        builder.HasKey(a => a.Id);

        // THE overbooking guarantee: a (trip, seat) can be permanently assigned only once.
        // Even under a race, the second INSERT fails — no seat is ever sold twice.
        builder.HasIndex(a => new { a.TripId, a.SeatNumber }).IsUnique();

        // FK → trip, Restrict: a trip with sold seats can't be hard-deleted (the BookingId FK
        // is configured on the Booking side as Cascade).
        builder.HasOne<Trip>().WithMany()
            .HasForeignKey(a => a.TripId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
