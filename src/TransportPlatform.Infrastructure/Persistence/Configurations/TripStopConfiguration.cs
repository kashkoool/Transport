using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.Infrastructure.Persistence.Configurations;

internal sealed class TripStopConfiguration : IEntityTypeConfiguration<TripStop>
{
    public void Configure(EntityTypeBuilder<TripStop> builder)
    {
        builder.ToTable("trip_stop");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(TripStop.MaxNameLength).IsRequired();
        builder.Property(s => s.Sequence).IsRequired();

        // Stops are always read in route order for a single trip; a (TripId, Sequence) index
        // serves the ordered fetch and enforces no two stops share a position on the same trip.
        builder.HasIndex(s => new { s.TripId, s.Sequence }).IsUnique();
    }
}
