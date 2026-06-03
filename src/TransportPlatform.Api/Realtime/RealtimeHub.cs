using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TransportPlatform.Api.Realtime;

/// <summary>
/// Authenticated real-time hub. Every connection joins its own <c>user:{id}</c> group (for
/// notifications); clients viewing a trip call <see cref="SubscribeToTrip"/> to also receive live
/// seat-availability updates for that <c>trip:{id}</c>.
/// </summary>
[Authorize]
public sealed class RealtimeHub : Hub
{
    /// <summary>Cap trip subscriptions per connection so a client can't fan out into unbounded groups.</summary>
    private const int MaxTripSubscriptions = 20;

    // Per-connection set of subscribed trip ids (Hub instances are per-invocation, so this lives in
    // connection state via the items dictionary on the underlying context).
    private HashSet<string> Subscriptions =>
        (HashSet<string>)(Context.Items["trips"] ??= new HashSet<string>(StringComparer.Ordinal));

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        await base.OnConnectedAsync();
    }

    public Task SubscribeToTrip(string tripId)
    {
        if (!Guid.TryParse(tripId, out var id))
            return Task.CompletedTask; // ignore malformed ids rather than create junk groups
        var key = id.ToString();
        if (Subscriptions.Contains(key))
            return Task.CompletedTask; // idempotent — already subscribed
        if (Subscriptions.Count >= MaxTripSubscriptions)
            return Task.CompletedTask; // over the per-connection cap → ignore
        Subscriptions.Add(key);
        return Groups.AddToGroupAsync(Context.ConnectionId, TripGroup(key));
    }

    public Task UnsubscribeFromTrip(string tripId)
    {
        if (!Guid.TryParse(tripId, out var id))
            return Task.CompletedTask;
        var key = id.ToString();
        Subscriptions.Remove(key);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, TripGroup(key));
    }

    public static string UserGroup(string userId) => $"user:{userId}";
    public static string TripGroup(string tripId) => $"trip:{tripId}";
}
