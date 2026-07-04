using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Application.Promotions;
using TransportPlatform.Domain.Bookings;

namespace TransportPlatform.Application.Bookings;

public sealed record PassengerInput(
    string FirstName,
    string LastName,
    int SeatNumber,
    string? DocumentType = null,
    string? DocumentNumber = null);

public sealed record CreateBookingCommand(
    Guid TripId,
    IReadOnlyList<PassengerInput> Passengers,
    string IdempotencyKey,
    string? PromoCode = null,
    string? ContactPhone = null);

public sealed record CreateBookingResult(Guid BookingId, string Reference, decimal TotalAmount, string Currency);

public sealed class CreateBookingValidator : AbstractValidator<CreateBookingCommand>
{
    /// <summary>Max passengers (= seats) per booking — mirrors the hold cap.</summary>
    public const int MaxPassengers = 10;

    public CreateBookingValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Passengers).NotEmpty();
        RuleFor(x => x.Passengers).Must(p => p.Count <= MaxPassengers)
            .WithMessage($"A booking can have at most {MaxPassengers} passengers.");
        RuleForEach(x => x.Passengers).ChildRules(p =>
        {
            p.RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            p.RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
            p.RuleFor(x => x.SeatNumber).InclusiveBetween(1, 1000);
            p.RuleFor(x => x.DocumentType).MaximumLength(Passenger.MaxDocumentLength);
            p.RuleFor(x => x.DocumentNumber).MaximumLength(Passenger.MaxDocumentLength);
        });
    }
}

/// <summary>
/// Turns the caller's active seat holds into a pending booking. Idempotent: replaying the
/// same Idempotency-Key returns the original booking instead of creating a duplicate. The
/// booking is bound to the authenticated caller (email + hold ownership come from the JWT).
/// </summary>
public sealed class CreateBookingHandler(
    IApplicationDbContext db, IClock clock, IReferenceGenerator references, ICurrentUser currentUser)
{
    public async Task<CreateBookingResult> HandleAsync(CreateBookingCommand command, CancellationToken ct)
    {
        var customerEmail = currentUser.RequireEmail();

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

        // Every requested seat must be covered by an active hold owned by THIS caller.
        var holds = await db.SeatHolds
            .Where(h => h.TripId == trip.Id
                        && seatNumbers.Contains(h.SeatNumber)
                        && h.HeldBy == customerEmail
                        && !h.Consumed
                        && h.ExpiresAtUtc > now)
            .ToListAsync(ct);

        if (holds.Count != seatNumbers.Count)
            throw new ConflictException("hold.missing",
                "Your seat hold has expired or is incomplete. Please reselect your seats.");

        var passengers = command.Passengers
            .Select(p => new Passenger(p.FirstName, p.LastName, p.SeatNumber, p.DocumentType, p.DocumentNumber))
            .ToList();

        // Optional promo code: validate + compute the discount (read-only here). The actual
        // redemption is an ATOMIC conditional UPDATE performed INSIDE the booking transaction below,
        // so if the booking insert fails the redemption rolls back with it (no leaked redemptions).
        decimal discount = 0;
        string? appliedPromo = null;
        Guid? promoId = null;
        if (!string.IsNullOrWhiteSpace(command.PromoCode))
        {
            var grossTotal = trip.Fare.Amount * passengers.Count;
            var (promo, computed) = await PromoEvaluation.EvaluateAsync(db, trip.CompanyId, command.PromoCode, grossTotal, now, ct);
            discount = computed;
            appliedPromo = promo.Code;
            promoId = promo.Id;
        }

        var booking = Booking.Create(
            trip.Id, customerEmail, references.NewBookingReference(),
            passengers, trip.Fare, command.IdempotencyKey, discount, appliedPromo, command.ContactPhone);

        foreach (var hold in holds)
            hold.AssignToBooking(booking.Id);

        db.Bookings.Add(booking);

        try
        {
            await db.ExecuteInTransactionAsync(async token =>
            {
                // Redeem the promo atomically with the same conditional-cap check; 0 rows affected
                // means it's no longer redeemable. Rolls back with the booking if the save fails.
                if (promoId is { } id)
                {
                    var redeemed = await db.PromoCodes
                        .Where(p => p.Id == id
                                    && p.Active
                                    && (p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)
                                    && (p.MaxRedemptions == null || p.RedemptionCount < p.MaxRedemptions))
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.RedemptionCount, p => p.RedemptionCount + 1), token);
                    if (redeemed == 0)
                        throw new ConflictException("promo.exhausted", "This promo code is no longer available.");
                }

                await db.SaveChangesAsync(token);
            }, ct);
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
