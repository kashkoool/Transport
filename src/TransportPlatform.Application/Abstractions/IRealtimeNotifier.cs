namespace TransportPlatform.Application.Abstractions;

/// <summary>
/// Pushes real-time messages to connected clients (implemented over SignalR in the API layer).
/// Kept as an Application abstraction so handlers/dispatchers stay free of the transport.
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>Push a notification to a single user's connections.</summary>
    Task NotifyUserAsync(Guid userId, object payload, CancellationToken cancellationToken = default);

    /// <summary>Broadcast a seat-availability change to everyone viewing a trip.</summary>
    Task BroadcastSeatUpdateAsync(Guid tripId, object payload, CancellationToken cancellationToken = default);
}
