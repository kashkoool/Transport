using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Notifications;

public sealed record MarkNotificationReadCommand(Guid NotificationId);

/// <summary>Marks one of the caller's notifications read (scoped to the caller).</summary>
public sealed class MarkNotificationReadHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task HandleAsync(MarkNotificationReadCommand command, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == command.NotificationId && n.RecipientUserId == userId, ct)
            ?? throw new NotFoundException("Notification", command.NotificationId);

        notification.MarkRead(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Marks all of the caller's unread notifications read.</summary>
public sealed class MarkAllNotificationsReadHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var unread = await db.Notifications
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .ToListAsync(ct);
        foreach (var n in unread)
            n.MarkRead(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}
