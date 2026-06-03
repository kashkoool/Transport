using Microsoft.AspNetCore.SignalR;
using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Api.Realtime;

/// <summary>
/// SignalR-backed <see cref="IRealtimeNotifier"/>. Delivery is best-effort: a transport failure is
/// logged and swallowed so it never fails (and re-triggers) the outbox event that produced it.
/// </summary>
public sealed class SignalRRealtimeNotifier(
    IHubContext<RealtimeHub> hub, ILogger<SignalRRealtimeNotifier> logger) : IRealtimeNotifier
{
    public async Task NotifyUserAsync(Guid userId, object payload, CancellationToken cancellationToken = default)
    {
        try
        {
            await hub.Clients.Group(RealtimeHub.UserGroup(userId.ToString()))
                .SendAsync("notification", payload, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPushFailed(logger, "notification", ex);
        }
    }

    public async Task BroadcastSeatUpdateAsync(Guid tripId, object payload, CancellationToken cancellationToken = default)
    {
        try
        {
            await hub.Clients.Group(RealtimeHub.TripGroup(tripId.ToString()))
                .SendAsync("seat-update", payload, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPushFailed(logger, "seat-update", ex);
        }
    }

    private static readonly Action<ILogger, string, Exception?> LogPushFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, nameof(LogPushFailed)),
            "Realtime push ({Kind}) failed.");
}
