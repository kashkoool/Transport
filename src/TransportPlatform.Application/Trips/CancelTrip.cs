using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Trips;

public sealed record CancelTripCommand(Guid TripId);

/// <summary>
/// A vendor manager cancels one of their OWN trips. Ownership is baked into the where clause
/// so another tenant's trip id resolves to "not found", never "forbidden" (no IDOR oracle).
/// </summary>
public sealed class CancelTripHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<TripDto> HandleAsync(CancelTripCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();

        var trip = await db.Trips
            .FirstOrDefaultAsync(t => t.Id == command.TripId && t.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Trip", command.TripId);

        trip.Cancel();
        await db.SaveChangesAsync(ct);

        return TripDto.From(trip);
    }
}
