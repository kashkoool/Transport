namespace TransportPlatform.Application.Notifications;

/// <summary>Read model for an in-app notification.</summary>
public sealed record NotificationDto(
    Guid Id, string Title, string Message, string Type, bool IsRead, DateTimeOffset CreatedAtUtc);
