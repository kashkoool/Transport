using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Domain.Bookings.Events;
using TransportPlatform.Infrastructure.Email;

namespace TransportPlatform.Infrastructure.Persistence;

/// <summary>
/// Routes outbox domain events to side effects. Currently: a booking-confirmation email on
/// <see cref="BookingConfirmedDomainEvent"/>. Unknown event types are a no-op (logged), so new
/// events don't break the publisher. A throw here is caught by the publisher and retried.
/// </summary>
public sealed class OutboxEventDispatcher(
    IEmailSender email,
    IOptions<EmailOptions> emailOptions,
    ILogger<OutboxEventDispatcher> logger) : IIntegrationEventDispatcher
{
    private static readonly string BookingConfirmedType = typeof(BookingConfirmedDomainEvent).FullName!;
    private readonly EmailOptions _email = emailOptions.Value;

    public async Task DispatchAsync(string eventType, string payload, CancellationToken cancellationToken = default)
    {
        if (eventType == BookingConfirmedType)
        {
            var evt = JsonSerializer.Deserialize<BookingConfirmedDomainEvent>(payload)
                      ?? throw new InvalidOperationException("Could not deserialize BookingConfirmedDomainEvent.");
            var message = EmailTemplates.BookingConfirmed(
                evt.CustomerEmail, evt.BookingReference, _email.BuildTicketUrl(evt.BookingId));
            await email.SendAsync(message, cancellationToken);
            return;
        }

        LogUnhandled(logger, eventType, null);
    }

    private static readonly Action<ILogger, string, Exception?> LogUnhandled =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, nameof(LogUnhandled)),
            "Outbox event type {EventType} has no dispatcher handler; skipping.");
}
