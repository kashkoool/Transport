using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;

namespace TransportPlatform.Application.Trips;

public sealed record TripManifestPassenger(string FirstName, string LastName, int SeatNumber, string BookingReference);

public sealed record TripManifestResult(
    Guid TripId,
    string Origin,
    string Destination,
    DateTimeOffset DepartureUtc,
    int TotalPassengers,
    IReadOnlyList<TripManifestPassenger> Passengers);

/// <summary>
/// The boarding list for one of the company's own trips: every confirmed passenger with their seat,
/// ordered by seat number, for the desk/driver to check people on. Company-scoped (a foreign trip
/// resolves to "not found", never "forbidden").
/// </summary>
public sealed class TripManifestHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<TripManifestResult> HandleAsync(Guid tripId, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();

        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == tripId && t.CompanyId == companyId, ct)
            ?? throw new NotFoundException("Trip", tripId);

        var bookings = await db.Bookings
            .Where(b => b.TripId == tripId && b.Status == BookingStatus.Confirmed)
            .Include(b => b.Passengers)
            .ToListAsync(ct);

        var passengers = bookings
            .SelectMany(b => b.Passengers.Select(p =>
                new TripManifestPassenger(p.FirstName, p.LastName, p.SeatNumber, b.Reference)))
            .OrderBy(p => p.SeatNumber)
            .ToList();

        return new TripManifestResult(
            trip.Id, trip.Origin, trip.Destination, trip.DepartureUtc, passengers.Count, passengers);
    }
}
