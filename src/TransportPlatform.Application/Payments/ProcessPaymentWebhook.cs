using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;

namespace TransportPlatform.Application.Payments;

public sealed record ProcessWebhookCommand(string Payload, IReadOnlyDictionary<string, string> Headers);

public sealed record ProcessWebhookResult(bool Handled, string Status);

/// <summary>
/// The authoritative payment outcome. Verifies the gateway signature, then atomically:
/// marks the payment, consumes the seat holds, and confirms the booking (which inserts the
/// permanent seat assignments — protected by the DB unique constraint). Idempotent: a
/// re-delivered webhook is a no-op.
/// </summary>
public sealed class ProcessPaymentWebhookHandler(IApplicationDbContext db, IPaymentGateway gateway)
{
    public async Task<ProcessWebhookResult> HandleAsync(ProcessWebhookCommand command, CancellationToken ct)
    {
        var webhook = await gateway.VerifyAndParseWebhookAsync(command.Payload, command.Headers, ct)
                      ?? throw new ConflictException("webhook.invalid_signature", "Webhook signature verification failed.");

        var booking = await db.Bookings
            .Include(b => b.Passengers)
            .FirstOrDefaultAsync(b => b.Reference == webhook.BookingReference, ct);
        if (booking is null)
            return new ProcessWebhookResult(false, "booking_not_found");

        if (booking.Status == BookingStatus.Confirmed)
            return new ProcessWebhookResult(true, "already_confirmed"); // idempotent

        var resultStatus = "ignored";

        await db.ExecuteInTransactionAsync(async token =>
        {
            var payment = await db.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.Id, token);

            if (webhook.Succeeded)
            {
                payment?.MarkCompleted(webhook.GatewayReference);

                // Consume any still-present holds. Tolerate a just-expired hold (sweeper race):
                // payment is authoritative, and the unique seat_assignment constraint below is
                // the real guard against a double-sell.
                var holds = await db.SeatHolds
                    .Where(h => h.BookingId == booking.Id && !h.Consumed)
                    .ToListAsync(token);
                foreach (var hold in holds)
                    hold.ConsumeOnConfirmation();

                booking.Confirm(); // creates seat_assignment rows + raises BookingConfirmedDomainEvent
                resultStatus = "confirmed";
            }
            else
            {
                payment?.MarkFailed();
                booking.Cancel();
                resultStatus = "failed";
            }

            await db.SaveChangesAsync(token);
        }, ct);

        return new ProcessWebhookResult(true, resultStatus);
    }
}
