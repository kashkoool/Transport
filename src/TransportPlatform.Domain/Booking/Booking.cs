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

    /// <summary>The promo code applied (if any) and the amount it took off the fare.</summary>
    public string? PromoCode { get; private set; }
    public decimal DiscountAmount { get; private set; }

    /// <summary>Optional contact phone for SMS on delay/cancellation. Null when not supplied.</summary>
    public string? ContactPhone { get; private set; }

    /// <summary>Why the booking was cancelled (e.g. "Customer cancellation"). Null unless cancelled.</summary>
    public string? CancellationReason { get; private set; }

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
        string idempotencyKey,
        decimal discountAmount = 0,
        string? promoCode = null,
        string? contactPhone = null)
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

        var gross = farePerSeat.Multiply(passengers.Count);
        var discount = Math.Clamp(discountAmount, 0, gross.Amount);
        var net = new Money(gross.Amount - discount, gross.Currency);

        var booking = new Booking(tripId, customerEmail.Trim().ToLowerInvariant(), reference, net, idempotencyKey)
        {
            DiscountAmount = discount,
            PromoCode = discount > 0 ? promoCode?.Trim().ToUpperInvariant() : null,
            ContactPhone = string.IsNullOrWhiteSpace(contactPhone) ? null : contactPhone.Trim(),
        };
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
        Raise(new BookingConfirmedDomainEvent(Id, TripId, Reference, CustomerEmail));
    }

    /// <summary>Cancel a not-yet-confirmed booking (releases it); idempotent if already cancelled.
    /// Confirmed bookings must go through <see cref="CancelConfirmed"/>.</summary>
    /// <param name="reason">Optional free-text reason recorded on the booking (e.g. "Customer cancellation").</param>
    public void Cancel(string? reason = null)
    {
        if (Status == BookingStatus.Cancelled)
            return; // idempotent
        // A Confirmed booking must go through CancelConfirmed (which orchestrates the refund).
        if (Status == BookingStatus.Confirmed)
            throw new DomainException("booking.already_confirmed", "Confirmed bookings require a refund flow.");
        // Any other non-pending state (Expired) is terminal and can't be cancelled here.
        if (Status != BookingStatus.PendingPayment)
            throw new DomainException("booking.not_pending", "Only a pending booking can be cancelled here.");
        Status = BookingStatus.Cancelled;
        SetCancellationReason(reason);
    }

    /// <summary>
    /// Cancel a CONFIRMED booking (the refund + seat-freeing is orchestrated by the handler).
    /// Raises the cancellation event so notifications/refund follow-ups can run.
    /// </summary>
    /// <param name="reason">Optional free-text reason recorded on the booking (e.g. "Operator cancellation").</param>
    public void CancelConfirmed(string? reason = null)
    {
        if (Status != BookingStatus.Confirmed)
            throw new DomainException("booking.not_confirmed", "Only a confirmed booking can be cancelled here.");
        Status = BookingStatus.Cancelled;
        SetCancellationReason(reason);
        Raise(new BookingCancelledDomainEvent(Id, TripId, Reference, CustomerEmail));
    }

    private void SetCancellationReason(string? reason) =>
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

    public void Expire()
    {
        if (Status == BookingStatus.PendingPayment)
            Status = BookingStatus.Expired;
    }

    /// <summary>
    /// Move a passenger from one seat to another within this trip (same fare, so no price change).
    /// Updates both the passenger's seat and the permanent seat assignment; the new assignment is
    /// still protected by the DB unique constraint against a concurrent claim.
    /// </summary>
    public void ChangeSeat(int fromSeat, int toSeat)
    {
        if (Status != BookingStatus.Confirmed)
            throw new DomainException("booking.not_confirmed", "Only a confirmed booking's seats can be changed.");
        if (fromSeat == toSeat)
            return; // no-op
        var passenger = _passengers.FirstOrDefault(p => p.SeatNumber == fromSeat)
            ?? throw new DomainException("booking.seat_not_found", $"This booking has no passenger on seat {fromSeat}.");
        if (_passengers.Any(p => p.SeatNumber == toSeat))
            throw new DomainException("booking.duplicate_seats", "Another passenger on this booking already has that seat.");

        passenger.MoveToSeat(toSeat);
        var assignment = _seatAssignments.FirstOrDefault(a => a.SeatNumber == fromSeat);
        if (assignment is not null)
        {
            _seatAssignments.Remove(assignment);
            _seatAssignments.Add(new SeatAssignment(TripId, toSeat, Id));
        }
    }

    /// <summary>Mark a confirmed passenger as a no-show (recorded after the trip departs). Terminal:
    /// the trip ran, so there is no refund and the seat is not re-released.</summary>
    public void MarkNoShow()
    {
        if (Status == BookingStatus.NoShow)
            return; // idempotent
        if (Status != BookingStatus.Confirmed)
            throw new DomainException("booking.not_confirmed", "Only a confirmed booking can be marked no-show.");
        Status = BookingStatus.NoShow;
    }
}
