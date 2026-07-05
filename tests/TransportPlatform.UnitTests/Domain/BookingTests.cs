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

    [Fact]
    public void A_confirmed_booking_can_be_marked_no_show()
    {
        var booking = NewBooking(1);
        booking.Confirm();

        booking.MarkNoShow();
        booking.MarkNoShow(); // idempotent

        booking.Status.Should().Be(BookingStatus.NoShow);
    }

    [Fact]
    public void A_pending_booking_cannot_be_marked_no_show()
    {
        var booking = NewBooking(1); // PendingPayment

        var act = () => booking.MarkNoShow();

        act.Should().Throw<DomainException>().Which.Code.Should().Be("booking.not_confirmed");
    }

    [Fact]
    public void ChangeSeat_moves_the_passenger_and_reassigns_the_seat()
    {
        var booking = NewBooking(4);
        booking.Confirm();

        booking.ChangeSeat(4, 9);

        booking.Passengers.Should().ContainSingle(p => p.SeatNumber == 9);
        booking.Passengers.Should().NotContain(p => p.SeatNumber == 4);
        booking.SeatAssignments.Should().ContainSingle(a => a.SeatNumber == 9);
        booking.SeatAssignments.Should().NotContain(a => a.SeatNumber == 4);
    }

    [Fact]
    public void ChangeSeat_is_rejected_before_confirmation()
    {
        var booking = NewBooking(4); // PendingPayment

        var act = () => booking.ChangeSeat(4, 9);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("booking.not_confirmed");
    }

    [Fact]
    public void ChangeSeat_rejects_a_seat_already_on_the_booking()
    {
        var booking = NewBooking(1, 2);
        booking.Confirm();

        var act = () => booking.ChangeSeat(1, 2);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("booking.duplicate_seats");
    }
}
