using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;

namespace TransportPlatform.Application.Bookings;

public sealed record ChangeSeatCommand(Guid BookingId, int FromSeat, int ToSeat);

public sealed record ChangeSeatResult(string Status, int FromSeat, int ToSeat);

public sealed class ChangeSeatValidator : AbstractValidator<ChangeSeatCommand>
{
    public ChangeSeatValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        // Upper bound caps a malicious huge value; the trip's real seat range is enforced later
        // (matches HoldSeatsValidator's 1..1000 convention).
        RuleFor(x => x.FromSeat).InclusiveBetween(1, 1000);
        RuleFor(x => x.ToSeat).InclusiveBetween(1, 1000);
        RuleFor(x => x.ToSeat).NotEqual(x => x.FromSeat)
            .WithMessage("The new seat must differ from the current seat.");
    }
}

/// <summary>
/// Customer moves one of their confirmed booking's passengers to a different free seat on the SAME
/// trip — the common "I'd rather sit by the window" change, with no price difference. Blocked once
/// the trip has departed. The new seat must be free (not assigned, not actively held); the
/// seat-assignment unique constraint is the final guard against a concurrent claim.
/// </summary>
public sealed class ChangeSeatHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<ChangeSeatResult> HandleAsync(ChangeSeatCommand command, CancellationToken ct)
    {
        var email = currentUser.RequireEmail();
        var booking = await db.Bookings
            .Include(b => b.Passengers)
            .Include(b => b.SeatAssignments)
            .FirstOrDefaultAsync(b => b.Id == command.BookingId && b.CustomerEmail == email, ct)
            ?? throw new NotFoundException("Booking", command.BookingId);

        if (booking.Status != BookingStatus.Confirmed)
            throw new ConflictException("booking.not_confirmed", "Only a confirmed booking's seat can be changed.");

        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == booking.TripId, ct)
            ?? throw new NotFoundException("Trip", booking.TripId);
        var now = clock.UtcNow;
        if (trip.DepartureUtc <= now)
            throw new ConflictException("booking.cancel_after_departure", "This trip has already departed.");
        trip.EnsureSeatsAreValid([command.ToSeat]);

        // The target seat must be free: not sold to another booking, not actively held online.
        var taken = await db.SeatAssignments
            .AnyAsync(a => a.TripId == trip.Id && a.SeatNumber == command.ToSeat && a.BookingId != booking.Id, ct);
        var held = await db.SeatHolds
            .AnyAsync(h => h.TripId == trip.Id && h.SeatNumber == command.ToSeat && !h.Consumed && h.ExpiresAtUtc > now, ct);
        if (taken || held)
            throw new ConflictException("seat.unavailable", "That seat is no longer available.");

        try
        {
            await db.ExecuteInTransactionAsync(async token =>
            {
                booking.ChangeSeat(command.FromSeat, command.ToSeat);
                await db.SaveChangesAsync(token);
            }, ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent booking claimed the new seat between the free-check and the write.
            throw new ConflictException("seat.unavailable", "That seat was just taken.");
        }

        return new ChangeSeatResult("changed", command.FromSeat, command.ToSeat);
    }
}
