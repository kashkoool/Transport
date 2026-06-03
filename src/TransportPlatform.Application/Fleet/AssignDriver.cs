using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Fleet;

public sealed record AssignDriverCommand(Guid BusId, Guid? DriverId);

/// <summary>
/// Assign (or clear, when DriverId is null) the driver of one of the manager's own buses. Both the
/// bus and the driver must belong to the caller's company — cross-tenant assignment is impossible.
/// </summary>
public sealed class AssignDriverHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<BusDto> HandleAsync(AssignDriverCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();

        var bus = await db.Buses.FirstOrDefaultAsync(b => b.Id == command.BusId && b.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Bus", command.BusId);

        if (command.DriverId is { } driverId)
        {
            var driverInCompany = await db.Drivers.AnyAsync(d => d.Id == driverId && d.CompanyId == companyId, ct);
            if (!driverInCompany)
                throw new NotFoundException("Driver", driverId);
        }

        bus.AssignDriver(command.DriverId);
        await db.SaveChangesAsync(ct);
        return BusDto.From(bus);
    }
}
