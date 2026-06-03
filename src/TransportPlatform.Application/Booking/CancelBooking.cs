using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.Application.Bookings;

public sealed record CancelBookingCommand(Guid BookingId);

public sealed record CancelBookingResult(string Status, bool RefundInitiated);

/// <summary>
/// Customer self-cancel. A pending booking is simply released (holds freed). A confirmed booking
/// can be cancelled only at least 48 hours before departure; it frees the seats and, if a payment
/// was captured, records and processes a refund. The booking is always scoped to the caller.
/// </summary>
public sealed class CancelBookingHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IPaymentGateway gateway)
{
    /// <summary>Customers can self-cancel a confirmed booking only up to this long before departure.</summary>
    public static readonly TimeSpan CancellationWindow = TimeSpan.FromHours(48);

    public async Task<CancelBookingResult> HandleAsync(CancelBookingCommand command, CancellationToken ct)
    {
        var email = currentUser.RequireEmail();
        var booking = await db.Bookings.FirstOrDefaultAsync(
            b => b.Id == command.BookingId && b.CustomerEmail == email, ct)
            ?? throw new NotFoundException("Booking", command.BookingId);

        switch (booking.Status)
        {
            case BookingStatus.Cancelled:
                return new CancelBookingResult("already_cancelled", false);

            case BookingStatus.PendingPayment:
                await db.ExecuteInTransactionAsync(async token =>
                {
                    var holds = await db.SeatHolds.Where(h => h.BookingId == booking.Id).ToListAsync(token);
                    db.SeatHolds.RemoveRange(holds); // release the seats
                    booking.Cancel();
                    await db.SaveChangesAsync(token);
                }, ct);
                return new CancelBookingResult("cancelled", false);

            case BookingStatus.Confirmed:
                break; // handled below

            default:
                throw new ConflictException("booking.not_cancellable", "This booking can no longer be cancelled.");
        }

        // Confirmed → enforce the 48-hour window against the trip's departure.
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == booking.TripId, ct)
            ?? throw new NotFoundException("Trip", booking.TripId);
        if (trip.DepartureUtc - clock.UtcNow < CancellationWindow)
            throw new ConflictException("booking.cancel_window_closed",
                "Bookings can only be cancelled at least 48 hours before departure.");

        Guid? refundId = null;
        await db.ExecuteInTransactionAsync(async token =>
        {
            var assignments = await db.SeatAssignments.Where(a => a.BookingId == booking.Id).ToListAsync(token);
            db.SeatAssignments.RemoveRange(assignments); // free the seats

            var payment = await db.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.Id, token);
            if (payment is { Status: PaymentStatus.Completed })
            {
                var refund = new Refund(payment.Id, booking.Id, new Money(payment.Amount, payment.Currency),
                    "Customer cancellation", $"refund-{booking.Id:N}");
                db.Refunds.Add(refund);
                refundId = refund.Id;
            }

            booking.CancelConfirmed();
            await db.SaveChangesAsync(token);
        }, ct);

        // Process the refund with the gateway AFTER the cancellation commits — never hold a DB
        // transaction open across an external call. Best-effort: a failure leaves the refund
        // Pending (the booking is already cancelled) for a reconciliation job to retry later.
        var refundInitiated = false;
        if (refundId is { } id)
            refundInitiated = await RefundProcessing.TryProcessAsync(db, gateway, id, ct);

        return new CancelBookingResult("cancelled", refundInitiated);
    }
}
