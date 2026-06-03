using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Bookings.Events;

/// <summary>
/// Raised when a booking is confirmed (payment captured). Persisted to the outbox in the
/// same transaction as the booking, then published to drive notifications / tickets / etc.
/// </summary>
public sealed record BookingConfirmedDomainEvent(Guid BookingId, Guid TripId, string BookingReference, string CustomerEmail)
    : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
