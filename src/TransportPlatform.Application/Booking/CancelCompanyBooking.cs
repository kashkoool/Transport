using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Payments;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.Application.Bookings;

public sealed record CancelCompanyBookingCommand(Guid BookingId);

public sealed record CancelCompanyBookingResult(string Status, bool RefundInitiated);

/// <summary>
/// Staff/manager cancels one of their company's bookings (desk request or trip disruption) and
/// refunds it. Unlike customer self-cancel there is no 48h window. Cash payments are flagged for a
/// manual refund (a Pending refund); gateway payments are refunded through the gateway after commit.
/// </summary>
public sealed class CancelCompanyBookingHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IPaymentGateway gateway)
{
    public async Task<CancelCompanyBookingResult> HandleAsync(CancelCompanyBookingCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var booking = await db.Bookings.FirstOrDefaultAsync(
            b => b.Id == command.BookingId && db.Trips.Any(t => t.Id == b.TripId && t.CompanyId == companyId), ct)
            ?? throw new NotFoundException("Booking", command.BookingId);

        switch (booking.Status)
        {
            case BookingStatus.Cancelled:
                return new CancelCompanyBookingResult("already_cancelled", false);

            case BookingStatus.PendingPayment:
                await db.ExecuteInTransactionAsync(async token =>
                {
                    var holds = await db.SeatHolds.Where(h => h.BookingId == booking.Id).ToListAsync(token);
                    db.SeatHolds.RemoveRange(holds);
                    booking.Cancel("Operator cancellation");
                    await db.SaveChangesAsync(token);
                }, ct);
                return new CancelCompanyBookingResult("cancelled", false);

            case BookingStatus.Confirmed:
                break;

            default:
                throw new ConflictException("booking.not_cancellable", "This booking can no longer be cancelled.");
        }

        // Don't "cancel + refund" a booking on a trip that has already run — the customer has (or is)
        // travelling. A refund on a departed/in-progress/completed trip is a manual finance decision,
        // not a one-click desk cancel.
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == booking.TripId, ct)
            ?? throw new NotFoundException("Trip", booking.TripId);
        if (trip.Status is TripStatus.InProgress or TripStatus.Completed || trip.DepartureUtc <= clock.UtcNow)
            throw new ConflictException("booking.trip_departed",
                "This booking's trip has already departed and can no longer be cancelled at the desk.");

        Guid? refundId = null;
        var isCash = false;
        try
        {
            await db.ExecuteInTransactionAsync(async token =>
            {
                var assignments = await db.SeatAssignments.Where(a => a.BookingId == booking.Id).ToListAsync(token);
                db.SeatAssignments.RemoveRange(assignments); // free the seats

                var payment = await db.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.Id, token);
                if (payment is { Status: PaymentStatus.Completed })
                {
                    isCash = string.Equals(payment.Gateway, "Cash", StringComparison.OrdinalIgnoreCase);
                    var refund = new Refund(payment.Id, booking.Id, new Money(payment.Amount, payment.Currency),
                        "Operator cancellation", $"refund-{booking.Id:N}");
                    db.Refunds.Add(refund);
                    refundId = refund.Id;
                }

                booking.CancelConfirmed("Operator cancellation");
                await db.SaveChangesAsync(token);
            }, ct);
        }
        catch (DbUpdateException)
        {
            // Raced another cancel of the same confirmed booking (e.g. the customer self-cancelled at
            // the same moment). The refund's unique idempotency key collides, the transaction rolls
            // back atomically, and exactly one cancel + refund wins — return the idempotent shape.
            return new CancelCompanyBookingResult("already_cancelled", false);
        }

        // Cash is refunded manually at the desk → leave it Pending. Gateway payments are refunded
        // through the gateway after the transaction commits.
        var refundInitiated = false;
        if (refundId is { } id && !isCash)
            refundInitiated = await RefundProcessing.TryProcessAsync(db, gateway, id, ct);

        return new CancelCompanyBookingResult("cancelled", refundInitiated);
    }
}
