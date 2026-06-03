using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Notifications;

public sealed record ListNotificationsQuery(int? Page, int? Limit);

/// <summary>Lists the caller's own notifications, newest first.</summary>
public sealed class ListNotificationsHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<PagedResult<NotificationDto>> HandleAsync(ListNotificationsQuery query, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var page = new PageRequest(query.Page, query.Limit);

        var owned = db.Notifications.Where(n => n.RecipientUserId == userId);
        var total = await owned.CountAsync(ct);
        var items = await owned
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip(page.Skip)
            .Take(page.Limit)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Message, n.Type, n.IsRead, n.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<NotificationDto>(items, total, page.Page, page.Limit);
    }
}

/// <summary>Count of the caller's unread notifications (for the bell badge).</summary>
public sealed class UnreadCountHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public Task<int> HandleAsync(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        return db.Notifications.CountAsync(n => n.RecipientUserId == userId && !n.IsRead, ct);
    }
}
