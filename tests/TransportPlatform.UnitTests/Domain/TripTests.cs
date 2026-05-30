using FluentAssertions;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.UnitTests.Domain;

public class TripTests
{
    private static Trip NewTrip(int seats = 40) => new(
        companyId: Guid.NewGuid(),
        busId: Guid.NewGuid(),
        origin: "Damascus",
        destination: "Latakia",
        departureUtc: DateTimeOffset.UtcNow.AddDays(1),
        arrivalUtc: DateTimeOffset.UtcNow.AddDays(1).AddHours(4),
        seatCount: seats,
        fare: new Money(50_000m, "SYP"));

    [Fact]
    public void Create_with_same_origin_and_destination_is_rejected()
    {
        var act = () => new Trip(Guid.NewGuid(), Guid.NewGuid(), "Homs", "Homs",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            40, new Money(10m, "SYP"));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("trip.route_invalid");
    }

    [Fact]
    public void Create_with_arrival_before_departure_is_rejected()
    {
        var act = () => new Trip(Guid.NewGuid(), Guid.NewGuid(), "A", "B",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(-1),
            40, new Money(10m, "SYP"));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("trip.time_invalid");
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(40, true)]
    [InlineData(41, false)]
    public void IsValidSeat_respects_capacity(int seat, bool expected) =>
        NewTrip().IsValidSeat(seat).Should().Be(expected);

    [Fact]
    public void EnsureBookable_rejects_a_departed_trip()
    {
        var trip = NewTrip();
        var act = () => trip.EnsureBookable(DateTimeOffset.UtcNow.AddDays(2));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("trip.departed");
    }

    [Fact]
    public void EnsureBookable_rejects_a_cancelled_trip()
    {
        var trip = NewTrip();
        trip.Cancel();
        var act = () => trip.EnsureBookable(DateTimeOffset.UtcNow);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("trip.not_bookable");
    }
}
