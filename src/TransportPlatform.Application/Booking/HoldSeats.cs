using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;

namespace TransportPlatform.Application.Bookings;

public sealed record HoldSeatsCommand(Guid TripId, IReadOnlyList<int> SeatNumbers, string HeldBy);

public sealed record HoldSeatsResult(IReadOnlyList<Guid> HoldIds, DateTimeOffset ExpiresAtUtc);

public sealed class HoldSeatsValidator : AbstractValidator<HoldSeatsCommand>
{
    public HoldSeatsValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.HeldBy).NotEmpty();
        RuleFor(x => x.SeatNumbers).NotEmpty();
        RuleForEach(x => x.SeatNumbers).GreaterThan(0);
        RuleFor(x => x.SeatNumbers)
            .Must(s => s.Distinct().Count() == s.Count)
            .WithMessage("Duplicate seat numbers are not allowed.");
    }
}

/// <summary>
/// Reserves seats for ~10 minutes while the customer pays. The UNIQUE (trip_id, seat_number)
/// constraint on seat_hold is the concurrency lock — if two requests race for the same seat,
/// exactly one INSERT wins and the loser gets a clean 409.
/// </summary>
public sealed class HoldSeatsHandler(IApplicationDbContext db, IClock clock)
{
    public static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(10);

    public async Task<HoldSeatsResult> HandleAsync(HoldSeatsCommand command, CancellationToken ct)
    {
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == command.TripId, ct)
                   ?? throw new NotFoundException("Trip", command.TripId);

        var now = clock.UtcNow;
        trip.EnsureBookable(now);
        trip.EnsureSeatsAreValid(command.SeatNumbers);

        // Reject seats already permanently assigned.
        var assigned = await db.SeatAssignments
            .Where(a => a.TripId == trip.Id && command.SeatNumbers.Contains(a.SeatNumber))
            .Select(a => a.SeatNumber)
            .ToListAsync(ct);
        if (assigned.Count > 0)
            throw new ConflictException("seat.already_booked",
                $"Seat(s) already booked: {string.Join(", ", assigned)}.");

        var expiresAt = now.Add(HoldDuration);
        var holds = command.SeatNumbers
            .Select(seat => new SeatHold(trip.Id, seat, command.HeldBy, expiresAt))
            .ToList();

        db.SeatHolds.AddRange(holds);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique-constraint violation => another request holds one of these seats right now.
            throw new ConflictException("seat.just_taken",
                "One or more of the selected seats were just taken. Please choose different seats.");
        }

        return new HoldSeatsResult(holds.Select(h => h.Id).ToList(), expiresAt);
    }
}
