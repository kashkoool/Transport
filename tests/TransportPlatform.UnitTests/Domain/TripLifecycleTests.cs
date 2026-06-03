using FluentAssertions;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.UnitTests.Domain;

/// <summary>Revert + seat-count-sync invariants added for the bus/trip business rules.</summary>
public class TripLifecycleTests
{
    private static Trip NewTrip() => new(
        Guid.NewGuid(), Guid.NewGuid(), "Damascus", "Aleppo",
        DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(4),
        40, new Money(50_000m, "SYP"));

    [Fact]
    public void Revert_restores_a_cancelled_trip_to_scheduled()
    {
        var trip = NewTrip();
        trip.Cancel();
        trip.Revert();
        trip.Status.Should().Be(TripStatus.Scheduled);
    }

    [Fact]
    public void Revert_is_rejected_unless_the_trip_is_cancelled()
    {
        var trip = NewTrip(); // Scheduled
        var act = trip.Revert;
        act.Should().Throw<DomainException>().Which.Code.Should().Be("trip.not_revertable");
    }

    [Fact]
    public void SyncSeatCount_updates_a_scheduled_trip()
    {
        var trip = NewTrip();
        trip.SyncSeatCount(52);
        trip.SeatCount.Should().Be(52);
    }

    [Fact]
    public void SyncSeatCount_rejects_a_non_positive_count()
    {
        var trip = NewTrip();
        var act = () => trip.SyncSeatCount(0);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("trip.seats_invalid");
    }

    [Fact]
    public void SyncSeatCount_is_blocked_once_the_trip_is_no_longer_scheduled()
    {
        var trip = NewTrip();
        trip.Start();
        var act = () => trip.SyncSeatCount(52);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("trip.not_editable");
    }
}
