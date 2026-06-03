using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.Application.Trips;

/// <summary>
/// Bus-schedule conflict detection: a bus may not be on two scheduled trips whose time windows
/// overlap. Two windows [d1,a1) and [d2,a2) overlap iff d1 &lt; a2 and d2 &lt; a1. Only Scheduled
/// trips count (cancelled/completed don't reserve the bus).
/// </summary>
internal static class BusSchedule
{
    public static Task<bool> HasOverlapAsync(
        IApplicationDbContext db, Guid companyId, Guid busId,
        DateTimeOffset departUtc, DateTimeOffset arrivalUtc, Guid? excludeTripId, CancellationToken ct) =>
        db.Trips.AnyAsync(t =>
            t.BusId == busId
            && t.CompanyId == companyId
            && t.Status == TripStatus.Scheduled
            && (excludeTripId == null || t.Id != excludeTripId)
            && t.DepartureUtc < arrivalUtc
            && t.ArrivalUtc > departUtc, ct);

    public static async Task EnsureBusFreeAsync(
        IApplicationDbContext db, Guid companyId, Guid busId,
        DateTimeOffset departUtc, DateTimeOffset arrivalUtc, Guid? excludeTripId, CancellationToken ct)
    {
        if (await HasOverlapAsync(db, companyId, busId, departUtc, arrivalUtc, excludeTripId, ct))
            throw new ConflictException("bus.overlapping_trip",
                "This bus is already scheduled on an overlapping trip.");
    }
}
