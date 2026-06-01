using FluentAssertions;
using TransportPlatform.Application.Bookings;
using TransportPlatform.Application.Trips;

namespace TransportPlatform.UnitTests.Application;

public class ValidatorTests
{
    [Fact]
    public void HoldSeats_rejects_more_than_the_max_seats()
    {
        var tooMany = Enumerable.Range(1, HoldSeatsValidator.MaxSeatsPerHold + 1).ToList();
        var result = new HoldSeatsValidator().Validate(new HoldSeatsCommand(Guid.NewGuid(), tooMany));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void HoldSeats_rejects_duplicate_seats()
    {
        var result = new HoldSeatsValidator().Validate(new HoldSeatsCommand(Guid.NewGuid(), [5, 5]));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void HoldSeats_accepts_a_normal_party()
    {
        var result = new HoldSeatsValidator().Validate(new HoldSeatsCommand(Guid.NewGuid(), [3, 4, 5]));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateBooking_rejects_more_than_the_max_passengers()
    {
        var passengers = Enumerable.Range(1, CreateBookingValidator.MaxPassengers + 1)
            .Select(i => new PassengerInput("First", "Last", i)).ToList();
        var result = new CreateBookingValidator().Validate(
            new CreateBookingCommand(Guid.NewGuid(), passengers, "idem-key"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SearchTrips_rejects_empty_or_overlong_origin()
    {
        var validator = new SearchTripsValidator();
        validator.Validate(new SearchTripsQuery("", "Latakia", default)).IsValid.Should().BeFalse();
        validator.Validate(new SearchTripsQuery(new string('x', 121), "Latakia", default)).IsValid.Should().BeFalse();
        validator.Validate(new SearchTripsQuery("Damascus", "Latakia", default)).IsValid.Should().BeTrue();
    }
}
