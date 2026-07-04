using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Fleet;

namespace TransportPlatform.Application.Fleet;

public sealed record AddBusCommand(string BusNumber, int SeatCount, BusType Type, string? Model,
    int SeatsPerRow = Bus.DefaultSeatsPerRow, string? LicensePlate = null, DateTimeOffset? InsuranceExpiryUtc = null);

public sealed class AddBusValidator : AbstractValidator<AddBusCommand>
{
    public AddBusValidator()
    {
        RuleFor(x => x.BusNumber).NotEmpty().MaximumLength(40);
        RuleFor(x => x.SeatCount).GreaterThan(0).LessThanOrEqualTo(120);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Model).MaximumLength(100);
        RuleFor(x => x.SeatsPerRow).InclusiveBetween(1, Bus.MaxSeatsPerRow);
        RuleFor(x => x.LicensePlate).MaximumLength(20);
    }
}

/// <summary>
/// A vendor manager adds a bus to their OWN company. The company id comes from the caller's
/// identity (never the request body), so a manager can't create buses for another tenant.
/// </summary>
public sealed class AddBusHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<BusDto> HandleAsync(AddBusCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var bus = new Bus(companyId, command.BusNumber, command.SeatCount, command.Type, command.Model,
            command.SeatsPerRow, command.LicensePlate, command.InsuranceExpiryUtc);
        db.Buses.Add(bus);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique index on (company_id, bus_number).
            throw new ConflictException("bus.number_taken", "A bus with this number already exists in your fleet.");
        }

        return BusDto.From(bus);
    }
}
