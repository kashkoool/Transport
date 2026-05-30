using FluentAssertions;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Bookings.Events;
using TransportPlatform.Domain.Common;

namespace TransportPlatform.UnitTests.Domain;

public class BookingTests
{
    private static readonly Money Fare = new(50_000m, "SYP");

    private static Booking NewBooking(params int[] seats)
    {
        var passengers = seats
            .Select((s, i) => new Passenger($"First{i}", $"Last{i}", s))
            .ToList();
        return Booking.Create(Guid.NewGuid(), "user@example.com", "BK-123", passengers, Fare, Guid.NewGuid().ToString());
    }

    [Fact]
    public void Create_computes_total_as_fare_times_passengers()
    {
        var booking = NewBooking(1, 2, 3);
        booking.Total.Amount.Should().Be(150_000m);
        booking.Total.Currency.Should().Be("SYP");
    }

    [Fact]
    public void Create_rejects_duplicate_seats()
    {
        var act = () => NewBooking(5, 5);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("booking.duplicate_seats");
    }

    [Fact]
    public void Create_rejects_zero_passengers()
    {
        var act = () => Booking.Create(Guid.NewGuid(), "u@e.com", "BK", [], Fare, "idem-key");
        act.Should().Throw<DomainException>().Which.Code.Should().Be("booking.no_passengers");
    }

    [Fact]
    public void Confirm_creates_one_seat_assignment_per_passenger_and_raises_event()
    {
        var booking = NewBooking(1, 2);

        booking.Confirm();

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.SeatAssignments.Should().HaveCount(2);
        booking.SeatAssignments.Select(a => a.SeatNumber).Should().BeEquivalentTo([1, 2]);
        booking.DomainEvents.OfType<BookingConfirmedDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Confirm_is_idempotent_for_duplicate_webhooks()
    {
        var booking = NewBooking(1);

        booking.Confirm();
        booking.Confirm(); // second webhook delivery

        booking.SeatAssignments.Should().HaveCount(1);
        booking.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public void Cancelling_a_confirmed_booking_requires_refund_flow()
    {
        var booking = NewBooking(1);
        booking.Confirm();

        var act = () => booking.Cancel();

        act.Should().Throw<DomainException>().Which.Code.Should().Be("booking.already_confirmed");
    }
}
