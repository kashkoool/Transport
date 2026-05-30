using TransportPlatform.Domain.Bookings.Events;
using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Bookings;

/// <summary>
/// A customer's reservation for a trip. Aggregate root over its passengers and seat
/// assignments. Drives the booking saga: PendingPayment → Confirmed (or Cancelled/Expired).
/// </summary>
public sealed class Booking : AggregateRoot
{
    private readonly List<Passenger> _passengers = [];
    private readonly List<SeatAssignment> _seatAssignments = [];

    public Guid TripId { get; private set; }
    public string CustomerEmail { get; private set; } = null!;
    public string Reference { get; private set; } = null!;
    public BookingStatus Status { get; private set; } = BookingStatus.PendingPayment;
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = "SYP";

    /// <summary>
    /// Caller-supplied dedupe key. A UNIQUE index means replaying the same create request
    /// (double-click, network retry) cannot create a second booking.
    /// </summary>
    public string IdempotencyKey { get; private set; } = null!;

    public IReadOnlyCollection<Passenger> Passengers => _passengers.AsReadOnly();
    public IReadOnlyCollection<SeatAssignment> SeatAssignments => _seatAssignments.AsReadOnly();

    public Money Total => new(TotalAmount, Currency);

    private Booking() { } // EF

    private Booking(Guid tripId, string customerEmail, string reference, Money total, string idempotencyKey)
    {
        TripId = tripId;
        CustomerEmail = customerEmail;
        Reference = reference;
        TotalAmount = total.Amount;
        Currency = total.Currency;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>
    /// Create a pending booking for a set of passengers. Each passenger occupies a distinct
    /// seat; the per-seat fare comes from the trip.
    /// </summary>
    public static Booking Create(
        Guid tripId,
        string customerEmail,
        string reference,
        IReadOnlyList<Passenger> passengers,
        Money farePerSeat,
        string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
            throw new DomainException("booking.email_required", "Customer email is required.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("booking.idempotency_required", "An idempotency key is required.");
        if (passengers.Count == 0)
            throw new DomainException("booking.no_passengers", "A booking needs at least one passenger.");

        var seats = passengers.Select(p => p.SeatNumber).ToList();
        if (seats.Distinct().Count() != seats.Count)
            throw new DomainException("booking.duplicate_seats", "Each passenger must have a distinct seat.");

        var total = farePerSeat.Multiply(passengers.Count);
        var booking = new Booking(tripId, customerEmail.Trim().ToLowerInvariant(), reference, total, idempotencyKey);
        booking._passengers.AddRange(passengers);
        return booking;
    }

    /// <summary>
    /// Confirm the booking once payment is captured: create the permanent seat assignments
    /// (protected by the DB unique constraint) and raise the confirmation event.
    /// </summary>
    public void Confirm()
    {
        if (Status == BookingStatus.Confirmed)
            return; // idempotent: a duplicate webhook must not double-confirm
        if (Status != BookingStatus.PendingPayment)
            throw new DomainException("booking.not_pending", "Only a pending booking can be confirmed.");

        foreach (var passenger in _passengers)
            _seatAssignments.Add(new SeatAssignment(TripId, passenger.SeatNumber, Id));

        Status = BookingStatus.Confirmed;
        Raise(new BookingConfirmedDomainEvent(Id, Reference, CustomerEmail));
    }

    public void Cancel()
    {
        if (Status == BookingStatus.Confirmed)
            throw new DomainException("booking.already_confirmed", "Confirmed bookings require a refund flow.");
        Status = BookingStatus.Cancelled;
    }

    public void Expire()
    {
        if (Status == BookingStatus.PendingPayment)
            Status = BookingStatus.Expired;
    }
}
