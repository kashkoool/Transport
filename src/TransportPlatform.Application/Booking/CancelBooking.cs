using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.Application.Bookings;

public sealed record CancelBookingCommand(Guid BookingId);

public sealed record CancelBookingResult(string Status, bool RefundInitiated, decimal RefundAmount, string Currency);

/// <summary>
/// Customer self-cancel. A pending booking is simply released (holds freed). A confirmed booking can
/// be cancelled any time before departure; how much is refunded follows the graduated
/// <see cref="RefundPolicy"/> (full ≥48h out, half 24–48h, none under 24h — the seat is still freed).
/// The booking is always scoped to the caller.
/// </summary>
public sealed class CancelBookingHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IPaymentGateway gateway)
{
    public async Task<CancelBookingResult> HandleAsync(CancelBookingCommand command, CancellationToken ct)
    {
        var email = currentUser.RequireEmail();
        var booking = await db.Bookings.FirstOrDefaultAsync(
            b => b.Id == command.BookingId && b.CustomerEmail == email, ct)
            ?? throw new NotFoundException("Booking", command.BookingId);

        switch (booking.Status)
        {
            case BookingStatus.Cancelled:
                return new CancelBookingResult("already_cancelled", false, 0, booking.Currency);

            case BookingStatus.PendingPayment:
                await db.ExecuteInTransactionAsync(async token =>
                {
                    var holds = await db.SeatHolds.Where(h => h.BookingId == booking.Id).ToListAsync(token);
                    db.SeatHolds.RemoveRange(holds); // release the seats
                    booking.Cancel("Customer cancellation");
                    await db.SaveChangesAsync(token);
                }, ct);
                return new CancelBookingResult("cancelled", false, 0, booking.Currency);

            case BookingStatus.Confirmed:
                break; // handled below

            default:
                throw new ConflictException("booking.not_cancellable", "This booking can no longer be cancelled.");
        }

        // Confirmed → the trip must not have departed; the refundable share is tiered by how far out
        // the cancellation is.
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == booking.TripId, ct)
            ?? throw new NotFoundException("Trip", booking.TripId);
        var timeToDeparture = trip.DepartureUtc - clock.UtcNow;
        if (timeToDeparture <= TimeSpan.Zero)
            throw new ConflictException("booking.cancel_after_departure",
                "This trip has already departed and can no longer be cancelled.");

        Guid? refundId = null;
        decimal refundAmount = 0;
        try
        {
            await db.ExecuteInTransactionAsync(async token =>
            {
                var assignments = await db.SeatAssignments.Where(a => a.BookingId == booking.Id).ToListAsync(token);
                db.SeatAssignments.RemoveRange(assignments); // free the seats

                var payment = await db.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.Id, token);
                if (payment is { Status: PaymentStatus.Completed })
                {
                    var refundable = RefundPolicy.RefundableAmount(new Money(payment.Amount, payment.Currency), timeToDeparture);
                    if (refundable.Amount > 0)
                    {
                        refundId = RefundProcessing.RecordRefund(db, payment, booking.Id, "Customer cancellation", refundable);
                        refundAmount = refundable.Amount;
                    }
                }

                booking.CancelConfirmed("Customer cancellation");
                await db.SaveChangesAsync(token);
            }, ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race with a concurrent cancel of the same confirmed booking (a customer
            // self-cancel and a desk-cancel firing together). The refund's unique idempotency key —
            // or the seat-assignment deletes — collide, so this transaction rolls back atomically and
            // exactly one cancellation + refund wins. Surface the idempotent shape, not a raw 500.
            return new CancelBookingResult("already_cancelled", false, 0, booking.Currency);
        }

        // Process the refund with the gateway AFTER the cancellation commits — never hold a DB
        // transaction open across an external call. Best-effort: a failure leaves the refund Pending
        // (the booking is already cancelled) for the reconciliation worker to retry later.
        var refundInitiated = false;
        if (refundId is { } id)
            refundInitiated = await RefundProcessing.TryProcessAsync(db, gateway, id, ct);

        return new CancelBookingResult("cancelled", refundInitiated, refundAmount, booking.Currency);
    }
}
