namespace TransportPlatform.Application.Abstractions;

/// <summary>
/// Dispatches an outbox-stored domain event (by its type name + JSON payload) to its side
/// effects — e.g. sending an email. The outbox publisher calls this; the implementation knows
/// which event types it handles and ignores the rest.
/// </summary>
public interface IIntegrationEventDispatcher
{
    Task DispatchAsync(string eventType, string payload, CancellationToken cancellationToken = default);
}
