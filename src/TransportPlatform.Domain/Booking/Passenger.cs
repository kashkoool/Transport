using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Bookings;

/// <summary>A traveller on a booking, occupying exactly one seat.</summary>
public sealed class Passenger : Entity
{
    public Guid BookingId { get; private set; }
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public int SeatNumber { get; private set; }

    private Passenger() { } // EF

    public Passenger(string firstName, string lastName, int seatNumber)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("passenger.name_required", "Passenger name is required.");
        if (seatNumber < 1)
            throw new DomainException("passenger.seat_invalid", "Seat number must be positive.");

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        SeatNumber = seatNumber;
    }
}
