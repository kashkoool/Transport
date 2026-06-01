using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.Application.Trips;

public sealed record ScheduleTripCommand(
    Guid BusId,
    string Origin,
    string Destination,
    DateTimeOffset DepartureUtc,
    DateTimeOffset ArrivalUtc,
    decimal Price,
    string Currency);

public sealed class ScheduleTripValidator : AbstractValidator<ScheduleTripCommand>
{
    public ScheduleTripValidator()
    {
        RuleFor(x => x.BusId).NotEmpty();
        RuleFor(x => x.Origin).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}

/// <summary>
/// A vendor manager schedules a trip on one of their OWN buses. The bus is looked up with
/// the company id baked into the where clause (IDOR-safe: another tenant's bus is simply
/// "not found"). Seat count is taken from the bus, never the request.
/// </summary>
public sealed class ScheduleTripHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<TripDto> HandleAsync(ScheduleTripCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();

        var bus = await db.Buses
            .FirstOrDefaultAsync(b => b.Id == command.BusId && b.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Bus", command.BusId);

        var trip = new Trip(
            companyId, bus.Id, command.Origin, command.Destination,
            command.DepartureUtc, command.ArrivalUtc, bus.SeatCount,
            new Money(command.Price, command.Currency));

        db.Trips.Add(trip);
        await db.SaveChangesAsync(ct);

        return TripDto.From(trip);
    }
}
