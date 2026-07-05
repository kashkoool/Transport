using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Trips.Events;

/// <summary>
/// Raised when a scheduled trip's times change (delay / reschedule). Drives a notification to every
/// confirmed passenger on the trip so they learn of the new departure.
/// </summary>
public sealed record TripRescheduledDomainEvent(
    Guid TripId, string Origin, string Destination, DateTimeOffset OldDepartureUtc, DateTimeOffset NewDepartureUtc)
    : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
