using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;

namespace TransportPlatform.Application.Bookings;

public sealed record MarkNoShowCommand(Guid BookingId);

public sealed record MarkNoShowResult(string Status);

/// <summary>
/// Staff/manager records that a confirmed passenger didn't board. Company-scoped (the booking's trip
/// must belong to the caller's company). Only valid once the trip has departed — you can't no-show a
/// trip that hasn't run. No refund is issued: the seat was reserved and the trip ran.
/// </summary>
public sealed class MarkNoShowHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<MarkNoShowResult> HandleAsync(MarkNoShowCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var booking = await db.Bookings.FirstOrDefaultAsync(
            b => b.Id == command.BookingId && db.Trips.Any(t => t.Id == b.TripId && t.CompanyId == companyId), ct)
            ?? throw new NotFoundException("Booking", command.BookingId);

        if (booking.Status == BookingStatus.NoShow)
            return new MarkNoShowResult("no_show"); // idempotent
        if (booking.Status != BookingStatus.Confirmed)
            throw new ConflictException("booking.not_confirmed", "Only a confirmed booking can be marked no-show.");

        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == booking.TripId, ct)
            ?? throw new NotFoundException("Trip", booking.TripId);
        if (trip.DepartureUtc > clock.UtcNow)
            throw new ConflictException("booking.not_departed",
                "A passenger can only be marked no-show after the trip has departed.");

        booking.MarkNoShow();
        await db.SaveChangesAsync(ct);
        return new MarkNoShowResult("no_show");
    }
}
