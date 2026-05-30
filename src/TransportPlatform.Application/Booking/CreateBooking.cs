using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;

namespace TransportPlatform.Application.Bookings;

public sealed record PassengerInput(string FirstName, string LastName, int SeatNumber);

public sealed record CreateBookingCommand(
    Guid TripId,
    string CustomerEmail,
    string HeldBy,
    IReadOnlyList<PassengerInput> Passengers,
    string IdempotencyKey);

public sealed record CreateBookingResult(Guid BookingId, string Reference, decimal TotalAmount, string Currency);

public sealed class CreateBookingValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.CustomerEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.HeldBy).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.Passengers).NotEmpty();
        RuleForEach(x => x.Passengers).ChildRules(p =>
        {
            p.RuleFor(x => x.FirstName).NotEmpty();
            p.RuleFor(x => x.LastName).NotEmpty();
            p.RuleFor(x => x.SeatNumber).GreaterThan(0);
        });
    }
}

/// <summary>
/// Turns active seat holds into a pending booking. Idempotent: replaying the same
/// Idempotency-Key returns the original booking instead of creating a duplicate.
/// </summary>
public sealed class CreateBookingHandler(IApplicationDbContext db, IClock clock, IReferenceGenerator references)
{
    public async Task<CreateBookingResult> HandleAsync(CreateBookingCommand command, CancellationToken ct)
    {
        // Fast idempotency path: already created for this key?
        var existing = await db.Bookings
            .FirstOrDefaultAsync(b => b.IdempotencyKey == command.IdempotencyKey, ct);
        if (existing is not null)
            return new CreateBookingResult(existing.Id, existing.Reference, existing.TotalAmount, existing.Currency);

        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == command.TripId, ct)
                   ?? throw new NotFoundException("Trip", command.TripId);

        var now = clock.UtcNow;
        trip.EnsureBookable(now);

        var seatNumbers = command.Passengers.Select(p => p.SeatNumber).ToList();
        trip.EnsureSeatsAreValid(seatNumbers);

        // Every requested seat must be covered by an active hold owned by this caller.
        var holds = await db.SeatHolds
            .Where(h => h.TripId == trip.Id
                        && seatNumbers.Contains(h.SeatNumber)
                        && h.HeldBy == command.HeldBy
                        && !h.Consumed
                        && h.ExpiresAtUtc > now)
            .ToListAsync(ct);

        if (holds.Count != seatNumbers.Count)
            throw new ConflictException("hold.missing",
                "Your seat hold has expired or is incomplete. Please reselect your seats.");

        var passengers = command.Passengers
            .Select(p => new Passenger(p.FirstName, p.LastName, p.SeatNumber))
            .ToList();

        var booking = Booking.Create(
            trip.Id, command.CustomerEmail, references.NewBookingReference(),
            passengers, trip.Fare, command.IdempotencyKey);

        foreach (var hold in holds)
            hold.AssignToBooking(booking.Id);

        db.Bookings.Add(booking);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Concurrent create with the same idempotency key won the race — return theirs.
            var winner = await db.Bookings
                .FirstOrDefaultAsync(b => b.IdempotencyKey == command.IdempotencyKey, ct);
            if (winner is not null)
                return new CreateBookingResult(winner.Id, winner.Reference, winner.TotalAmount, winner.Currency);
            throw new ConflictException("booking.conflict", "Could not create the booking, please retry.");
        }

        return new CreateBookingResult(booking.Id, booking.Reference, booking.TotalAmount, booking.Currency);
    }
}
