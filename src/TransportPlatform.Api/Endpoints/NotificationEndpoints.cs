using TransportPlatform.Api.Security;
using TransportPlatform.Application.Notifications;

namespace TransportPlatform.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        // Any authenticated user has a notification inbox (scoped to themselves in the handlers).
        var group = app.MapGroup("/api/notifications").WithTags("Notifications")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Sensitive);

        group.MapGet("/", async (int? page, int? limit, ListNotificationsHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListNotificationsQuery(page, limit), ct)))
        .WithName("ListNotifications")
        .WithSummary("List the caller's notifications (newest first).");

        group.MapGet("/unread-count", async (UnreadCountHandler handler, CancellationToken ct) =>
            Results.Ok(new { count = await handler.HandleAsync(ct) }))
        .WithName("UnreadNotificationCount")
        .WithSummary("Count the caller's unread notifications.");

        group.MapPost("/{id:guid}/read", async (Guid id, MarkNotificationReadHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new MarkNotificationReadCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("MarkNotificationRead")
        .WithSummary("Mark one notification read.");

        group.MapPost("/read-all", async (MarkAllNotificationsReadHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(ct);
            return Results.NoContent();
        })
        .WithName("MarkAllNotificationsRead")
        .WithSummary("Mark all the caller's notifications read.");

        group.MapDelete("/{id:guid}", async (Guid id, DeleteNotificationHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new DeleteNotificationCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("DeleteNotification")
        .WithSummary("Delete one of the caller's notifications.");

        return app;
    }
}
