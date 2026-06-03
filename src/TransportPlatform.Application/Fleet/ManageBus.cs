using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Fleet;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.Application.Fleet;

public sealed record UpdateBusCommand(Guid BusId, int SeatCount, BusType Type, string? Model, int SeatsPerRow = Bus.DefaultSeatsPerRow);

public sealed class UpdateBusValidator : AbstractValidator<UpdateBusCommand>
{
    public UpdateBusValidator()
    {
        RuleFor(x => x.BusId).NotEmpty();
        RuleFor(x => x.SeatCount).GreaterThan(0).LessThanOrEqualTo(120);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Model).MaximumLength(100);
        RuleFor(x => x.SeatsPerRow).InclusiveBetween(1, Bus.MaxSeatsPerRow);
    }
}

/// <summary>Edit one of the caller's own buses (company-scoped).</summary>
public sealed class UpdateBusHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<BusDto> HandleAsync(UpdateBusCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var bus = await db.Buses.FirstOrDefaultAsync(b => b.Id == command.BusId && b.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Bus", command.BusId);

        // The new capacity cascades to the bus's still-scheduled trips (a trip copies its bus's seat
        // count at schedule time). Block a reduction that would drop below an already-sold seat.
        var scheduledTripIds = await db.Trips
            .Where(t => t.BusId == bus.Id && t.Status == TripStatus.Scheduled)
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (scheduledTripIds.Count > 0)
        {
            var maxSoldSeat = await db.SeatAssignments
                .Where(a => scheduledTripIds.Contains(a.TripId))
                .Select(a => (int?)a.SeatNumber)
                .MaxAsync(ct) ?? 0;
            if (command.SeatCount < maxSoldSeat)
                throw new ConflictException("bus.seats_occupied",
                    $"Can't reduce to {command.SeatCount} seats — seat {maxSoldSeat} is already sold on a scheduled trip.");
        }

        bus.Update(command.SeatCount, command.Type, command.Model, command.SeatsPerRow);

        if (scheduledTripIds.Count > 0)
        {
            var trips = await db.Trips.Where(t => scheduledTripIds.Contains(t.Id)).ToListAsync(ct);
            foreach (var trip in trips)
                trip.SyncSeatCount(command.SeatCount);
        }

        await db.SaveChangesAsync(ct);
        return BusDto.From(bus);
    }
}

public sealed record DeleteBusCommand(Guid BusId);

/// <summary>Delete one of the caller's own buses — blocked while any trip references it.</summary>
public sealed class DeleteBusHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task HandleAsync(DeleteBusCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var bus = await db.Buses.FirstOrDefaultAsync(b => b.Id == command.BusId && b.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Bus", command.BusId);

        if (await db.Trips.AnyAsync(t => t.BusId == bus.Id, ct))
            throw new ConflictException("bus.in_use", "This bus is used by one or more trips and can't be deleted.");

        db.Buses.Remove(bus);
        await db.SaveChangesAsync(ct);
    }
}
