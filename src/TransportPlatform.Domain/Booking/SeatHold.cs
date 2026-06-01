using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Bookings;

/// <summary>
/// A short-lived reservation of a (trip, seat) while the customer completes payment.
/// A UNIQUE (trip_id, seat_number) constraint makes a double-hold impossible: the hold
/// IS the concurrency lock. Expired holds are swept by a background worker and freed.
/// </summary>
public sealed class SeatHold : AggregateRoot
{
    public Guid TripId { get; private set; }
    public int SeatNumber { get; private set; }
    public string HeldBy { get; private set; } = null!;
    public Guid? BookingId { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public bool Consumed { get; private set; }

    private SeatHold() { } // EF

    public SeatHold(Guid tripId, int seatNumber, string heldBy, DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(heldBy))
            throw new DomainException("hold.owner_required", "A hold must have an owner.");

        TripId = tripId;
        SeatNumber = seatNumber;
        HeldBy = heldBy;
        ExpiresAtUtc = expiresAtUtc;
    }

    public bool IsActive(DateTimeOffset nowUtc) => !Consumed && ExpiresAtUtc > nowUtc;

    /// <summary>Attach this hold to the booking that is being created from it.</summary>
    public void AssignToBooking(Guid bookingId) => BookingId = bookingId;

    /// <summary>Mark the hold as used once its booking is confirmed.</summary>
    public void Consume(DateTimeOffset nowUtc)
    {
        if (Consumed)
            throw new DomainException("hold.already_consumed", "This hold was already used.");
        if (ExpiresAtUtc <= nowUtc)
            throw new DomainException("hold.expired", "This seat hold has expired.");
        Consumed = true;
    }

    /// <summary>
    /// Consume the hold during payment confirmation, tolerating a just-expired hold. Once the
    /// gateway confirms payment the seat IS the customer's, so a hold that expired a moment
    /// before the webhook arrived (a sweeper race) must not fail confirmation. The unique
    /// (trip, seat) constraint on seat_assignment remains the real double-sell guard.
    /// </summary>
    public void ConsumeOnConfirmation()
    {
        if (Consumed)
            throw new DomainException("hold.already_consumed", "This hold was already used.");
        Consumed = true;
    }
}
