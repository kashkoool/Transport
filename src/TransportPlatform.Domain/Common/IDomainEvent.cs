namespace TransportPlatform.Domain.Common;

/// <summary>
/// A fact that has happened in the domain. Raised by aggregates and published
/// transactionally via the outbox so downstream effects (email, notifications,
/// real-time pushes) are never lost on a crash.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAtUtc { get; }
}
