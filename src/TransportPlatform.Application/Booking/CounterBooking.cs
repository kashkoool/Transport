using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.Application.Bookings;

public sealed record CounterBookingCommand(Guid TripId, IReadOnlyList<PassengerInput> Passengers, string CustomerEmail);

public sealed class CounterBookingValidator : AbstractValidator<CounterBookingCommand>
{
    public CounterBookingValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        // An email is required so the walk-in still gets an e-ticket + can look the booking up.
        RuleFor(x => x.CustomerEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.Passengers).NotEmpty();
        RuleFor(x => x.Passengers).Must(p => p.Count <= CreateBookingValidator.MaxPassengers)
            .WithMessage($"A booking can have at most {CreateBookingValidator.MaxPassengers} passengers.");
        RuleForEach(x => x.Passengers).ChildRules(p =>
        {
            p.RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            p.RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
            p.RuleFor(x => x.SeatNumber).InclusiveBetween(1, 1000);
        });
    }
}

/// <summary>
/// Staff/manager sells a ticket at the desk: an immediately-confirmed booking paid in cash. Scoped
/// to the operator's company (the trip must be theirs); the seat-assignment unique constraint makes
/// it impossible to oversell against a concurrent online booking.
/// </summary>
public sealed class CounterBookingHandler(
    IApplicationDbContext db, IClock clock, IReferenceGenerator references,
    ICurrentUser currentUser, IAppMetrics metrics)
{
    public async Task<CreateBookingResult> HandleAsync(CounterBookingCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == command.TripId && t.CompanyId == companyId, ct)
                   ?? throw new NotFoundException("Trip", command.TripId);

        var now = clock.UtcNow;
        trip.EnsureBookable(now);

        var seatNumbers = command.Passengers.Select(p => p.SeatNumber).ToList();
        trip.EnsureSeatsAreValid(seatNumbers);
        if (seatNumbers.Distinct().Count() != seatNumbers.Count)
            throw new ConflictException("booking.duplicate_seats", "Each passenger must have a distinct seat.");

        // Seats must be free: not already assigned, and not actively held by an online customer.
        var assigned = await db.SeatAssignments.AnyAsync(a => a.TripId == trip.Id && seatNumbers.Contains(a.SeatNumber), ct);
        var held = await db.SeatHolds.AnyAsync(
            h => h.TripId == trip.Id && seatNumbers.Contains(h.SeatNumber) && !h.Consumed && h.ExpiresAtUtc > now, ct);
        if (assigned || held)
            throw new ConflictException("seat.unavailable", "One or more selected seats are no longer available.");

        var passengers = command.Passengers
            .Select(p => new Passenger(p.FirstName, p.LastName, p.SeatNumber))
            .ToList();

        var booking = Booking.Create(
            trip.Id, command.CustomerEmail, references.NewBookingReference(),
            passengers, trip.Fare, $"counter-{Guid.NewGuid():N}");
        booking.Confirm(); // creates seat assignments + raises BookingConfirmedDomainEvent (e-ticket email)

        var payment = new Payment(booking.Id, "Cash", booking.Total, $"counter-{booking.Id:N}");
        payment.MarkCompleted($"CASH-{booking.Reference}");

        db.Bookings.Add(booking);
        db.Payments.Add(payment);

        try
        {
            await db.SaveChangesAsync(ct); // single transaction: booking + assignments + payment
        }
        catch (DbUpdateException)
        {
            // A concurrent sale grabbed one of these seats (unique seat_assignment index).
            throw new ConflictException("seat.unavailable", "One or more selected seats were just taken.");
        }

        metrics.PaymentSucceeded();
        metrics.BookingConfirmed();
        return new CreateBookingResult(booking.Id, booking.Reference, booking.TotalAmount, booking.Currency);
    }
}
