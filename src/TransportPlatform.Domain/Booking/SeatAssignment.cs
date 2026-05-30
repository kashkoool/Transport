using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Bookings;

/// <summary>
/// A permanent claim on a (trip, seat). A UNIQUE (trip_id, seat_number) constraint on this
/// table is the ultimate guarantee that a seat can never be sold twice — even if two
/// transactions race, the database rejects the second insert.
/// </summary>
public sealed class SeatAssignment : Entity
{
    public Guid TripId { get; private set; }
    public int SeatNumber { get; private set; }
    public Guid BookingId { get; private set; }

    private SeatAssignment() { } // EF

    public SeatAssignment(Guid tripId, int seatNumber, Guid bookingId)
    {
        TripId = tripId;
        SeatNumber = seatNumber;
        BookingId = bookingId;
    }
}
