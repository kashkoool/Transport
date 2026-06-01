using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;

namespace TransportPlatform.Application.Trips;

public sealed record CancelTripCommand(Guid TripId);

public sealed record CancelTripResult(
    TripDto Trip,
    int ReleasedHolds,
    int CancelledPendingBookings,
    int ConfirmedBookingsAffected);

/// <summary>
/// A vendor manager cancels one of their OWN trips. Ownership is baked into the where clause
/// so another tenant's trip id resolves to "not found", never "forbidden" (no IDOR oracle).
/// Cancellation cleans up dependent state in one transaction: active seat holds are released
/// and not-yet-paid bookings are cancelled. Confirmed (paid) bookings are counted and left
/// for the refund flow (deferred) — never silently voided.
/// </summary>
public sealed class CancelTripHandler(IApplicationDbContext db, ICurrentUser currentUser)
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

        await db.ExecuteInTransactionAsync(async token =>
        {
            trip.Cancel();

            // Free seats held by abandoned/in-flight checkouts.
            releasedHolds = await db.SeatHolds
                .Where(h => h.TripId == trip.Id && !h.Consumed)
                .ExecuteDeleteAsync(token);

            // Cancel not-yet-paid bookings (a BookingCancelled event could notify the customer).
            var pending = await db.Bookings
                .Where(b => b.TripId == trip.Id && b.Status == BookingStatus.PendingPayment)
                .ToListAsync(token);
            foreach (var booking in pending)
                booking.Cancel();
            cancelledPending = pending.Count;

            // Confirmed bookings are counted for the vendor's awareness; refunding them is the
            // (deferred) refund flow's job — we do not void a paid booking here.
            confirmedAffected = await db.Bookings
                .CountAsync(b => b.TripId == trip.Id && b.Status == BookingStatus.Confirmed, token);

            await db.SaveChangesAsync(token);
        }, ct);

        return new CancelTripResult(TripDto.From(trip), releasedHolds, cancelledPending, confirmedAffected);
    }
}
