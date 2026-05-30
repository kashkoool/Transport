using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Domain.Bookings;

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
    }
}
