using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Common;

namespace TransportPlatform.Application.Trips;

public sealed record UpdateTripCommand(
    Guid TripId, string Origin, string Destination,
    DateTimeOffset DepartureUtc, DateTimeOffset ArrivalUtc, decimal Price, string Currency);

public sealed class UpdateTripValidator : AbstractValidator<UpdateTripCommand>
{
    public UpdateTripValidator(IOptions<CurrencyOptions> currency)
    {
        var supported = currency.Value;
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.Origin).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).LessThanOrEqualTo(9_999_999.99m);
        RuleFor(x => x.Currency).NotEmpty().Length(3)
            .Must(supported.IsSupported)
            .WithMessage($"Currency must be one of: {string.Join(", ", supported.Supported)}.");
        RuleFor(x => x.ArrivalUtc).GreaterThan(x => x.DepartureUtc);
    }
}

/// <summary>Edit a scheduled trip — blocked once it has active (non-cancelled) bookings.</summary>
public sealed class UpdateTripHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<TripDto> HandleAsync(UpdateTripCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == command.TripId && t.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Trip", command.TripId);

        if (await db.Bookings.AnyAsync(b => b.TripId == trip.Id && b.Status != BookingStatus.Cancelled, ct))
            throw new ConflictException("trip.has_bookings", "This trip has bookings and can no longer be edited.");

        // New times must not clash with another scheduled trip on the same bus (exclude self).
        await BusSchedule.EnsureBusFreeAsync(
            db, companyId, trip.BusId, command.DepartureUtc, command.ArrivalUtc, excludeTripId: trip.Id, ct);

        trip.Update(command.Origin, command.Destination, command.DepartureUtc, command.ArrivalUtc,
            new Money(command.Price, command.Currency));
        await db.SaveChangesAsync(ct);
        return TripDto.From(trip);
    }
}

public sealed record DeleteTripCommand(Guid TripId);

/// <summary>Delete a trip — blocked if it has any bookings (cancel it instead).</summary>
public sealed class DeleteTripHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task HandleAsync(DeleteTripCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == command.TripId && t.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Trip", command.TripId);

        if (await db.Bookings.AnyAsync(b => b.TripId == trip.Id, ct))
            throw new ConflictException("trip.has_bookings", "This trip has bookings and can't be deleted — cancel it instead.");

        db.Trips.Remove(trip);
        await db.SaveChangesAsync(ct);
    }
}

public sealed record StartTripCommand(Guid TripId);

public sealed class StartTripHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<TripDto> HandleAsync(StartTripCommand command, CancellationToken ct)
    {
        var trip = await db.LoadOwnTripAsync(currentUser, command.TripId, ct);
        trip.Start(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return TripDto.From(trip);
    }
}

public sealed record CompleteTripCommand(Guid TripId);

public sealed class CompleteTripHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<TripDto> HandleAsync(CompleteTripCommand command, CancellationToken ct)
    {
        var trip = await db.LoadOwnTripAsync(currentUser, command.TripId, ct);
        trip.Complete(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return TripDto.From(trip);
    }
}

public sealed record RevertTripCommand(Guid TripId);

/// <summary>
/// Re-activate a cancelled trip (Cancelled → Scheduled). Allowed only when no confirmed bookings
/// remain on it and the bus is free again over the trip's window — otherwise the route would
/// double-book the bus. Cancellation already released holds and cancelled pending bookings, so
/// there is nothing to restore beyond the status.
/// </summary>
public sealed class RevertTripHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<TripDto> HandleAsync(RevertTripCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var trip = await db.LoadOwnTripAsync(currentUser, command.TripId, ct);

        if (await db.Bookings.AnyAsync(b => b.TripId == trip.Id && b.Status == BookingStatus.Confirmed, ct))
            throw new ConflictException("trip.has_confirmed_bookings",
                "This trip has confirmed bookings and can't be reverted.");

        await BusSchedule.EnsureBusFreeAsync(
            db, companyId, trip.BusId, trip.DepartureUtc, trip.ArrivalUtc, excludeTripId: trip.Id, ct);

        trip.Revert();
        await db.SaveChangesAsync(ct);
        return TripDto.From(trip);
    }
}

internal static class ManageTripExtensions
{
    /// <summary>Load a trip that belongs to the caller's company, or 404.</summary>
    public static async Task<Domain.Trips.Trip> LoadOwnTripAsync(
        this IApplicationDbContext db, ICurrentUser currentUser, Guid tripId, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        return await db.Trips.FirstOrDefaultAsync(t => t.Id == tripId && t.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Trip", tripId);
    }
}
