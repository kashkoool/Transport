using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Companies;

namespace TransportPlatform.Application.Bookings;

public sealed record HoldSeatsCommand(Guid TripId, IReadOnlyList<int> SeatNumbers);

public sealed record HoldSeatsResult(IReadOnlyList<Guid> HoldIds, DateTimeOffset ExpiresAtUtc);

public sealed class HoldSeatsValidator : AbstractValidator<HoldSeatsCommand>
{
    /// <summary>Max seats one customer can hold at once — a booking party, not a bulk grab.</summary>
    public const int MaxSeatsPerHold = 10;

    public HoldSeatsValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.SeatNumbers).NotEmpty();
        RuleFor(x => x.SeatNumbers).Must(s => s.Count <= MaxSeatsPerHold)
            .WithMessage($"You can hold at most {MaxSeatsPerHold} seats at once.");
        // Upper bound caps a malicious huge value; the trip's real seat range is checked later.
        RuleForEach(x => x.SeatNumbers).InclusiveBetween(1, 1000);
        RuleFor(x => x.SeatNumbers)
            .Must(s => s.Distinct().Count() == s.Count)
            .WithMessage("Duplicate seat numbers are not allowed.");
    }
}

/// <summary>
/// Reserves seats for ~10 minutes while the customer pays. The UNIQUE (trip_id, seat_number)
/// constraint on seat_hold is the concurrency lock — if two requests race for the same seat,
/// exactly one INSERT wins and the loser gets a clean 409. The hold owner is the authenticated
/// caller, never a request-body value.
/// </summary>
public sealed class HoldSeatsHandler(IApplicationDbContext db, IClock clock, ICurrentUser currentUser)
{
    public static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(10);

    public async Task<HoldSeatsResult> HandleAsync(HoldSeatsCommand command, CancellationToken ct)
    {
        var heldBy = currentUser.RequireEmail();

        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == command.TripId, ct)
                   ?? throw new NotFoundException("Trip", command.TripId);

        var now = clock.UtcNow;
        trip.EnsureBookable(now);
        trip.EnsureSeatsAreValid(command.SeatNumbers);

        // Defense in depth: a suspended vendor's trips must not be bookable even via a stale id.
        var companyActive = await db.Companies
            .AnyAsync(c => c.Id == trip.CompanyId && c.Status == CompanyStatus.Active, ct);
        if (!companyActive)
            throw new ConflictException("company.suspended", "This trip is not available for booking.");

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
            .Select(seat => new SeatHold(trip.Id, seat, heldBy, expiresAt))
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
