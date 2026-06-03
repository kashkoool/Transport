using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.Application.Trips;

public sealed record TripStopInput(string Name, DateTimeOffset? ArrivalUtc, DateTimeOffset? DepartureUtc);

public sealed record SetTripStopsCommand(Guid TripId, IReadOnlyList<TripStopInput> Stops);

public sealed record TripStopDto(int Sequence, string Name, DateTimeOffset? ArrivalUtc, DateTimeOffset? DepartureUtc)
{
    public static TripStopDto From(TripStop s) => new(s.Sequence, s.Name, s.ArrivalUtc, s.DepartureUtc);
}

public sealed class SetTripStopsValidator : AbstractValidator<SetTripStopsCommand>
{
    /// <summary>A route realistically has a bounded number of waypoints; cap it so the payload is bounded.</summary>
    public const int MaxStops = 50;

    public SetTripStopsValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.Stops).NotNull();
        RuleFor(x => x.Stops).Must(s => s.Count <= MaxStops)
            .WithMessage($"A trip can have at most {MaxStops} stops.");
        RuleForEach(x => x.Stops).ChildRules(s =>
        {
            s.RuleFor(x => x.Name).NotEmpty().MaximumLength(TripStop.MaxNameLength);
            s.RuleFor(x => x.DepartureUtc)
                .GreaterThanOrEqualTo(x => x.ArrivalUtc!.Value)
                .When(x => x.ArrivalUtc.HasValue && x.DepartureUtc.HasValue)
                .WithMessage("Stop departure cannot be before its arrival.");
        });
    }
}

/// <summary>Replace the waypoints on one of the caller's own trips (company-scoped).</summary>
public sealed class SetTripStopsHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<TripStopDto>> HandleAsync(SetTripStopsCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var trip = await db.Trips
            .Include(t => t.Stops)
            .FirstOrDefaultAsync(t => t.Id == command.TripId && t.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Trip", command.TripId);

        trip.SetStops(command.Stops.Select(s => (s.Name, s.ArrivalUtc, s.DepartureUtc)));
        await db.SaveChangesAsync(ct);

        return trip.Stops.OrderBy(s => s.Sequence).Select(TripStopDto.From).ToList();
    }
}

/// <summary>The ordered list of a trip's waypoints. Public read used to show the route.</summary>
public sealed class ListTripStopsHandler(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<TripStopDto>> HandleAsync(Guid tripId, CancellationToken ct)
    {
        if (!await db.Trips.AsNoTracking().AnyAsync(t => t.Id == tripId, ct))
            throw new NotFoundException("Trip", tripId);

        return await db.Trips.AsNoTracking()
            .Where(t => t.Id == tripId)
            .SelectMany(t => t.Stops)
            .OrderBy(s => s.Sequence)
            .Select(s => new TripStopDto(s.Sequence, s.Name, s.ArrivalUtc, s.DepartureUtc))
            .ToListAsync(ct);
    }
}
