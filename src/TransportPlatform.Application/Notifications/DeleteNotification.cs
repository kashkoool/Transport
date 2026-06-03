using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Notifications;

public sealed record DeleteNotificationCommand(Guid NotificationId);

/// <summary>Deletes one of the caller's own notifications (scoped to the caller; 404 otherwise).</summary>
public sealed class DeleteNotificationHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task HandleAsync(DeleteNotificationCommand command, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == command.NotificationId && n.RecipientUserId == userId, ct)
            ?? throw new NotFoundException("Notification", command.NotificationId);

        db.Notifications.Remove(notification);
        await db.SaveChangesAsync(ct);
    }
}
