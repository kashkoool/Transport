using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Bookings;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.Application.Trips;

public sealed record CancelTripCommand(Guid TripId);

public sealed record CancelTripResult(
    TripDto Trip,
    int ReleasedHolds,
    int CancelledPendingBookings,
    int ConfirmedBookingsAffected,
    int RefundsIssued);

/// <summary>
/// A vendor manager cancels one of their OWN trips. Ownership is baked into the where clause
/// so another tenant's trip id resolves to "not found", never "forbidden" (no IDOR oracle).
/// In one transaction: the trip is cancelled, active seat holds are released, not-yet-paid bookings
/// are cancelled, and every CONFIRMED (paid) booking is cancelled with its seats freed and a
/// full refund recorded — then the refunds are processed with the gateway after commit. A paid
/// customer is never silently left charged with a dead seat.
/// </summary>
public sealed class CancelTripHandler(IApplicationDbContext db, ICurrentUser currentUser, IPaymentGateway gateway)
{
    public async Task<CancelTripResult> HandleAsync(CancelTripCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();

        var trip = await db.Trips
            .FirstOrDefaultAsync(t => t.Id == command.TripId && t.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Trip", command.TripId);

        var releasedHolds = 0;
        var cancelledPending = 0;
        var confirmedAffected = 0;
        var refundIds = new List<Guid>();

        await db.ExecuteInTransactionAsync(async token =>
        {
            trip.Cancel();

            // Free seats held by abandoned/in-flight checkouts.
            releasedHolds = await db.SeatHolds
                .Where(h => h.TripId == trip.Id && !h.Consumed)
                .ExecuteDeleteAsync(token);

            // Cancel not-yet-paid bookings.
            var pending = await db.Bookings
                .Where(b => b.TripId == trip.Id && b.Status == BookingStatus.PendingPayment)
                .ToListAsync(token);
            foreach (var booking in pending)
                booking.Cancel("Trip cancelled by operator");
            cancelledPending = pending.Count;

            // Confirmed (paid) bookings: cancel each (raises BookingCancelledDomainEvent → the
            // customer is notified), free its seats, and record a refund for any captured payment.
            var confirmed = await db.Bookings
                .Where(b => b.TripId == trip.Id && b.Status == BookingStatus.Confirmed)
                .ToListAsync(token);
            confirmedAffected = confirmed.Count;

            if (confirmed.Count > 0)
            {
                var bookingIds = confirmed.Select(b => b.Id).ToList();

                var assignments = await db.SeatAssignments
                    .Where(a => bookingIds.Contains(a.BookingId)).ToListAsync(token);
                db.SeatAssignments.RemoveRange(assignments); // free the seats

                var payments = await db.Payments
                    .Where(p => bookingIds.Contains(p.BookingId)).ToListAsync(token);
                // Bookings already refunded by a racing customer-cancel — don't double-record.
                var alreadyRefunded = (await db.Refunds
                        .Where(r => bookingIds.Contains(r.BookingId))
                        .Select(r => r.BookingId).ToListAsync(token))
                    .ToHashSet();

                foreach (var booking in confirmed)
                {
                    booking.CancelConfirmed("Trip cancelled by operator");
                    var payment = payments.FirstOrDefault(p => p.BookingId == booking.Id);
                    if (payment is { Status: PaymentStatus.Completed } && !alreadyRefunded.Contains(booking.Id))
                        refundIds.Add(RefundProcessing.RecordRefund(db, payment, booking.Id, "Trip cancelled by operator"));
                }
            }

            await db.SaveChangesAsync(token);
        }, ct);

        // Process each recorded refund with the gateway AFTER the transaction commits (never hold a
        // DB transaction open across an external call). A gateway failure leaves the refund Pending
        // for the reconciliation worker; the trip cancellation already stands.
        var refundsIssued = 0;
        foreach (var refundId in refundIds)
            if (await RefundProcessing.TryProcessAsync(db, gateway, refundId, ct))
                refundsIssued++;

        return new CancelTripResult(
            TripDto.From(trip), releasedHolds, cancelledPending, confirmedAffected, refundsIssued);
    }
}
