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

    // Mirror the persisted column limits so invalid state is rejected up front (not at SaveChanges).
    public const int MaxTitleLength = 200;
    public const int MaxMessageLength = 2000;
    public const int MaxTypeLength = 20;

    public Notification(Guid recipientUserId, string title, string message, string type = "info")
    {
        if (recipientUserId == Guid.Empty)
            throw new DomainException("notification.recipient_required", "A notification needs a recipient.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("notification.title_required", "A notification needs a title.");

        var trimmedTitle = title.Trim();
        var trimmedMessage = (message ?? string.Empty).Trim();
        var resolvedType = string.IsNullOrWhiteSpace(type) ? "info" : type.Trim();

        if (trimmedTitle.Length > MaxTitleLength)
            throw new DomainException("notification.title_too_long", $"Title cannot exceed {MaxTitleLength} characters.");
        if (trimmedMessage.Length > MaxMessageLength)
            throw new DomainException("notification.message_too_long", $"Message cannot exceed {MaxMessageLength} characters.");
        if (resolvedType.Length > MaxTypeLength)
            throw new DomainException("notification.type_too_long", $"Type cannot exceed {MaxTypeLength} characters.");

        RecipientUserId = recipientUserId;
        Title = trimmedTitle;
        Message = trimmedMessage;
        Type = resolvedType;
    }

    public void MarkRead(DateTimeOffset now)
    {
        if (IsRead)
            return;
        IsRead = true;
        ReadAtUtc = now;
    }
}
