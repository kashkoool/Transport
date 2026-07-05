using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Bookings;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Payments;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.Application.Payments;

public sealed record ProcessWebhookCommand(string Payload, IReadOnlyDictionary<string, string> Headers);

public sealed record ProcessWebhookResult(bool Handled, string Status);

/// <summary>
/// The authoritative payment outcome. Verifies the gateway signature, then confirms the booking —
/// but only after re-checking, at confirm time, that (a) the captured amount equals the booking
/// total, and (b) the trip is still bookable. If confirmation is unsafe (wrong amount, dead trip,
/// or the seat was resold while the hold lapsed), the captured money is refunded rather than left
/// stuck — never money-in with no valid seat. Idempotent: a re-delivered webhook is a no-op.
/// </summary>
public sealed class ProcessPaymentWebhookHandler(IApplicationDbContext db, IPaymentGateway gateway, IAppMetrics metrics)
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

        // ── The booking was cancelled/expired before this (successful) payment could confirm it —
        //    refund the captured money rather than confirm a dead booking (ghost-confirm: a customer
        //    cancelled their pending booking in the same instant the gateway captured). ──
        if (webhook.Succeeded && booking.Status != BookingStatus.PendingPayment)
        {
            await CompensateAsync(booking, webhook.GatewayReference,
                webhook.Amount is { } a ? new Money(a, webhook.Currency ?? booking.Currency) : null,
                "Booking already cancelled");
            metrics.PaymentFailed();
            return new ProcessWebhookResult(true, "booking_not_pending");
        }

        // ── Failure event → mark the payment failed and release the pending booking. ──
        if (!webhook.Succeeded)
        {
            await db.ExecuteInTransactionAsync(async token =>
            {
                var payment = await db.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.Id, token);
                payment?.MarkFailed();
                booking.Cancel();
                await db.SaveChangesAsync(token);
            }, ct);
            metrics.PaymentFailed();
            return new ProcessWebhookResult(true, "failed");
        }

        // ── Amount verification: a signature-valid webhook for the WRONG amount must not confirm. ──
        if (webhook.Amount is { } captured
            && (captured != booking.TotalAmount
                || (webhook.Currency is { } cur && !string.Equals(cur, booking.Currency, StringComparison.OrdinalIgnoreCase))))
        {
            // Money was captured for the wrong amount — refund exactly what was captured, don't confirm.
            await CompensateAsync(booking, webhook.GatewayReference,
                new Money(captured, webhook.Currency ?? booking.Currency), "Captured amount mismatch");
            metrics.PaymentFailed();
            return new ProcessWebhookResult(true, "amount_mismatch");
        }

        // ── Trip must still be bookable at confirm time (it may have been cancelled/started since). ──
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == booking.TripId, ct);
        if (trip is null || trip.Status != TripStatus.Scheduled)
        {
            await CompensateAsync(booking, webhook.GatewayReference, capturedOverride: null, "Trip no longer available");
            metrics.PaymentFailed();
            return new ProcessWebhookResult(true, "trip_not_bookable");
        }

        // ── Confirm: consume holds and create the permanent seat assignments (unique-index guarded). ──
        var confirmed = false;
        try
        {
            await db.ExecuteInTransactionAsync(async token =>
            {
                // Re-read the booking's committed status inside the transaction. If a customer
                // cancelled it between our initial read and now (ghost-confirm), don't confirm —
                // leave confirmed=false so the captured money is refunded below.
                var current = await db.Bookings.AsNoTracking()
                    .Where(b => b.Id == booking.Id).Select(b => b.Status).FirstAsync(token);
                if (current != BookingStatus.PendingPayment)
                    return;

                var payment = await db.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.Id, token);
                payment?.MarkCompleted(webhook.GatewayReference);

                var holds = await db.SeatHolds
                    .Where(h => h.BookingId == booking.Id && !h.Consumed)
                    .ToListAsync(token);
                foreach (var hold in holds)
                    hold.ConsumeOnConfirmation();

                booking.Confirm(); // inserts seat_assignment rows + raises BookingConfirmedDomainEvent
                await db.SaveChangesAsync(token);
                confirmed = true;
            }, ct);
        }
        catch (DbUpdateException)
        {
            // The only expected failure is a seat_assignment unique violation: the seat was resold
            // after this booking's hold lapsed. Confirm one really occurred (don't mask an unrelated
            // DB error), then refund the paid customer rather than leaving them charged with no seat.
            var seatNumbers = booking.Passengers.Select(p => p.SeatNumber).ToList();
            var clash = await db.SeatAssignments.AsNoTracking()
                .AnyAsync(a => a.TripId == booking.TripId && seatNumbers.Contains(a.SeatNumber), ct);
            if (!clash)
                throw;

            await CompensateAsync(booking, webhook.GatewayReference, capturedOverride: null, "Seat no longer available");
            metrics.PaymentFailed();
            return new ProcessWebhookResult(true, "seat_unavailable");
        }

        if (!confirmed)
        {
            // Cancelled concurrently — refund the captured money instead of confirming a dead booking.
            await CompensateAsync(booking, webhook.GatewayReference,
                webhook.Amount is { } a ? new Money(a, webhook.Currency ?? booking.Currency) : null,
                "Booking cancelled during payment");
            metrics.PaymentFailed();
            return new ProcessWebhookResult(true, "cancelled_during_payment");
        }

        metrics.PaymentSucceeded();
        metrics.BookingConfirmed();
        return new ProcessWebhookResult(true, "confirmed");

        // ── Local compensation: record the capture, refund it, and release the booking. ──
        async Task CompensateAsync(Booking b, string gatewayRef, Money? capturedOverride, string reason)
        {
            Guid? refundId = null;
            await db.ExecuteInTransactionAsync(async token =>
            {
                var payment = await db.Payments.FirstOrDefaultAsync(p => p.BookingId == b.Id, token);
                if (payment is not null)
                {
                    payment.MarkCompleted(gatewayRef); // attach the capture ref so the refund can target it
                    var alreadyRefunded = await db.Refunds.AnyAsync(r => r.BookingId == b.Id, token);
                    if (!alreadyRefunded)
                        refundId = RefundProcessing.RecordRefund(db, payment, b.Id, reason, capturedOverride);
                }
                b.Cancel(); // PendingPayment → Cancelled
                await db.SaveChangesAsync(token);
            }, ct);

            if (refundId is { } id)
                await RefundProcessing.TryProcessAsync(db, gateway, id, ct);
        }
    }
}
