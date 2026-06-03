using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Fleet;

namespace TransportPlatform.Application.Fleet;

public sealed record UpdateBusCommand(Guid BusId, int SeatCount, BusType Type, string? Model);

public sealed class UpdateBusValidator : AbstractValidator<UpdateBusCommand>
{
    public UpdateBusValidator()
    {
        RuleFor(x => x.BusId).NotEmpty();
        RuleFor(x => x.SeatCount).GreaterThan(0).LessThanOrEqualTo(120);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Model).MaximumLength(100);
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

        bus.Update(command.SeatCount, command.Type, command.Model);
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
