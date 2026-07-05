using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Trips;

public sealed record RescheduleTripCommand(Guid TripId, DateTimeOffset DepartureUtc, DateTimeOffset ArrivalUtc);

public sealed class RescheduleTripValidator : AbstractValidator<RescheduleTripCommand>
{
    public RescheduleTripValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        // Arrival must be after departure. The "departure in the future" rule is time-dependent and
        // stays in the handler (needs IClock); this catches the ordering bug at the edge.
        RuleFor(x => x.ArrivalUtc).GreaterThan(x => x.DepartureUtc)
            .WithMessage("Arrival must be after departure.");
    }
}

/// <summary>
/// Delay / reschedule one of the company's own scheduled trips to new times. Validates the new
/// departure is in the future and the bus is free at the new window (no double-booking), then
/// reschedules — which raises an event that notifies every confirmed passenger of the change.
/// </summary>
public sealed class RescheduleTripHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<TripDto> HandleAsync(RescheduleTripCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();

        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == command.TripId && t.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Trip", command.TripId);

        if (command.DepartureUtc <= clock.UtcNow)
            throw new ConflictException("trip.departure_past", "The new departure time must be in the future.");

        // The bus must be free at the new window (excluding this trip itself).
        await BusSchedule.EnsureBusFreeAsync(
            db, companyId, trip.BusId, command.DepartureUtc, command.ArrivalUtc, trip.Id, ct);

        trip.Reschedule(command.DepartureUtc, command.ArrivalUtc);
        await db.SaveChangesAsync(ct);

        return TripDto.From(trip);
    }
}
