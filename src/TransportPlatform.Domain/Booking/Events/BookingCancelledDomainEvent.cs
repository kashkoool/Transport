using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Bookings.Events;

/// <summary>
/// Raised when a confirmed booking is cancelled (e.g. customer self-cancel within the window).
/// Drives the cancellation notification/email and any refund follow-up.
/// </summary>
public sealed record BookingCancelledDomainEvent(Guid BookingId, Guid TripId, string BookingReference, string CustomerEmail)
    : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
