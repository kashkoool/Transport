namespace TransportPlatform.Domain.Bookings;

/// <summary>
/// Booking saga states: a booking starts pending payment and only becomes confirmed
/// once the gateway reports success. See ARCHITECTURE.md (seat-hold → pay → confirm).
/// </summary>
public enum BookingStatus
{
    PendingPayment = 0,
    Confirmed = 1,
    Cancelled = 2,
    Expired = 3,

    /// <summary>Confirmed passenger who didn't board (recorded by staff after departure). Terminal;
    /// the trip ran so there is no refund, but it closes the booking's reconciliation loop.</summary>
    NoShow = 4,
}
