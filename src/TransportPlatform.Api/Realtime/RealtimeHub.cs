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
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        await base.OnConnectedAsync();
    }

    public Task SubscribeToTrip(string tripId) =>
        Guid.TryParse(tripId, out var id)
            ? Groups.AddToGroupAsync(Context.ConnectionId, TripGroup(id.ToString()))
            : Task.CompletedTask; // ignore malformed ids rather than create junk groups

    public Task UnsubscribeFromTrip(string tripId) =>
        Guid.TryParse(tripId, out var id)
            ? Groups.RemoveFromGroupAsync(Context.ConnectionId, TripGroup(id.ToString()))
            : Task.CompletedTask;

    public static string UserGroup(string userId) => $"user:{userId}";
    public static string TripGroup(string tripId) => $"trip:{tripId}";
}
