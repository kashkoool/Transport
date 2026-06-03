using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Trips;

public sealed record SeatMapDto(
    Guid TripId,
    int SeatCount,
    int SeatsPerRow,
    IReadOnlyList<int> TakenSeats);

/// <summary>
/// The seating layout for a trip plus the seats that are currently unavailable (confirmed
/// assignments + active, unexpired holds). Public read used to render the seat picker; no PII
/// is exposed — only seat numbers. Availability is computed with set queries (no N+1).
/// </summary>
public sealed class GetSeatMapHandler(IApplicationDbContext db, IClock clock)
{
    public async Task<SeatMapDto> HandleAsync(Guid tripId, CancellationToken ct)
    {
        var trip = await db.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tripId, ct)
                   ?? throw new NotFoundException("Trip", tripId);

        // Seats-per-row lives on the bus; default the layout if the bus row is ever missing.
        var seatsPerRow = await db.Buses.AsNoTracking()
            .Where(b => b.Id == trip.BusId)
            .Select(b => (int?)b.SeatsPerRow)
            .FirstOrDefaultAsync(ct) ?? Domain.Fleet.Bus.DefaultSeatsPerRow;

        var now = clock.UtcNow;

        var assignedSeats = await db.SeatAssignments.AsNoTracking()
            .Where(a => a.TripId == tripId)
            .Select(a => a.SeatNumber)
            .ToListAsync(ct);

        var heldSeats = await db.SeatHolds.AsNoTracking()
            .Where(h => h.TripId == tripId && !h.Consumed && h.ExpiresAtUtc > now)
            .Select(h => h.SeatNumber)
            .ToListAsync(ct);

        var takenSeats = assignedSeats.Concat(heldSeats).Distinct().OrderBy(s => s).ToList();

        return new SeatMapDto(trip.Id, trip.SeatCount, seatsPerRow, takenSeats);
    }
}
