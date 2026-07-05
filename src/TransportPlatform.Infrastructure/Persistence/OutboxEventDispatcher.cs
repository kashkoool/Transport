using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Bookings.Events;
using TransportPlatform.Domain.Notifications;
using TransportPlatform.Domain.Trips.Events;
using TransportPlatform.Infrastructure.Email;

namespace TransportPlatform.Infrastructure.Persistence;

/// <summary>
/// Routes outbox domain events to side effects: a confirmation email + an in-app notification on
/// booking confirmed, an in-app notification on booking cancelled, and a live seat-availability
/// broadcast for the trip in both cases. Unknown event types are a no-op (logged). A throw here is
/// caught by the publisher and retried, so the realtime pushes are best-effort in the notifier.
/// </summary>
public sealed class OutboxEventDispatcher(
    IEmailSender email,
    IOptions<EmailOptions> emailOptions,
    IApplicationDbContext db,
    IIdentityService identity,
    IRealtimeNotifier realtime,
    ILogger<OutboxEventDispatcher> logger) : IIntegrationEventDispatcher
{
    private static readonly string BookingConfirmedType = typeof(BookingConfirmedDomainEvent).FullName!;
    private static readonly string BookingCancelledType = typeof(BookingCancelledDomainEvent).FullName!;
    private static readonly string TripRescheduledType = typeof(TripRescheduledDomainEvent).FullName!;
    private readonly EmailOptions _email = emailOptions.Value;

    public async Task DispatchAsync(string eventType, string payload, CancellationToken cancellationToken = default)
    {
        if (eventType == BookingConfirmedType)
        {
            var evt = Deserialize<BookingConfirmedDomainEvent>(payload);
            // Localize to the customer's stored preference; unknown/counter bookings → English.
            var lang = await identity.GetUserLanguageByEmailAsync(evt.CustomerEmail, cancellationToken);
            var message = EmailTemplates.BookingConfirmed(
                evt.CustomerEmail, evt.BookingReference, _email.BuildTicketUrl(evt.BookingId), lang);
            await email.SendAsync(message, cancellationToken);
            await NotifyCustomerAsync(evt.CustomerEmail, "Booking confirmed",
                $"Your booking {evt.BookingReference} is confirmed.", "success", cancellationToken);
            await BroadcastSeatUpdateAsync(evt.TripId, cancellationToken);
            return;
        }

        if (eventType == BookingCancelledType)
        {
            var evt = Deserialize<BookingCancelledDomainEvent>(payload);
            await NotifyCustomerAsync(evt.CustomerEmail, "Booking cancelled",
                $"Your booking {evt.BookingReference} was cancelled. Any payment will be refunded.", "warning", cancellationToken);
            await BroadcastSeatUpdateAsync(evt.TripId, cancellationToken);
            return;
        }

        if (eventType == TripRescheduledType)
        {
            var evt = Deserialize<TripRescheduledDomainEvent>(payload);
            // Notify every confirmed passenger on the trip of the new departure time.
            var affected = await db.Bookings
                .Where(b => b.TripId == evt.TripId && b.Status == BookingStatus.Confirmed)
                .Select(b => new { b.Reference, b.CustomerEmail })
                .ToListAsync(cancellationToken);
            foreach (var booking in affected)
                await NotifyCustomerAsync(booking.CustomerEmail, "Trip rescheduled",
                    $"Your trip {evt.Origin} → {evt.Destination} (booking {booking.Reference}) now departs " +
                    $"{evt.NewDepartureUtc.UtcDateTime:yyyy-MM-dd HH:mm} UTC.", "warning", cancellationToken);
            return;
        }

        LogUnhandled(logger, eventType, null);
    }

    private Task BroadcastSeatUpdateAsync(Guid tripId, CancellationToken ct) =>
        // Outbox rows written before TripId existed deserialize to Guid.Empty — skip the no-op broadcast.
        tripId == Guid.Empty
            ? Task.CompletedTask
            : realtime.BroadcastSeatUpdateAsync(tripId, new { tripId }, ct);

    private async Task NotifyCustomerAsync(string recipientEmail, string title, string message, string type, CancellationToken ct)
    {
        var userId = await identity.FindUserIdByEmailAsync(recipientEmail, ct);
        if (userId is not { } id)
            return; // e.g. a counter booking with no customer account

        var notification = new Notification(id, title, message, type);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
        await realtime.NotifyUserAsync(id, ToDto(notification), ct);
    }

    private static object ToDto(Notification n) => new
    {
        id = n.Id,
        title = n.Title,
        message = n.Message,
        type = n.Type,
        isRead = n.IsRead,
        createdAtUtc = n.CreatedAtUtc,
    };

    private static T Deserialize<T>(string payload) =>
        JsonSerializer.Deserialize<T>(payload)
            ?? throw new InvalidOperationException($"Could not deserialize {typeof(T).Name}.");

    private static readonly Action<ILogger, string, Exception?> LogUnhandled =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, nameof(LogUnhandled)),
            "Outbox event type {EventType} has no dispatcher handler; skipping.");
}
