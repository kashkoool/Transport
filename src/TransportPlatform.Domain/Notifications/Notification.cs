using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Notifications;

/// <summary>
/// An in-app notification for a single user (customer, manager, staff or admin). Created as a side
/// effect of domain events (booking confirmed/cancelled) or admin → company messages, and pushed
/// live over SignalR. CreatedAtUtc is stamped by the auditing interceptor.
/// </summary>
public sealed class Notification : AggregateRoot
{
    public Guid RecipientUserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public string Type { get; private set; } = "info"; // info | success | warning | alert
    public bool IsRead { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }

    private Notification() { } // EF

    public Notification(Guid recipientUserId, string title, string message, string type = "info")
    {
        if (recipientUserId == Guid.Empty)
            throw new DomainException("notification.recipient_required", "A notification needs a recipient.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("notification.title_required", "A notification needs a title.");

        RecipientUserId = recipientUserId;
        Title = title.Trim();
        Message = (message ?? string.Empty).Trim();
        Type = string.IsNullOrWhiteSpace(type) ? "info" : type.Trim();
    }

    public void MarkRead(DateTimeOffset now)
    {
        if (IsRead)
            return;
        IsRead = true;
        ReadAtUtc = now;
    }
}
