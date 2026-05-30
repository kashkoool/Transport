using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Bookings;

public sealed record GetTicketQuery(Guid BookingId);

public sealed record TicketPassenger(string FirstName, string LastName, int SeatNumber);

public sealed record TicketResult(
    Guid BookingId,
    string Reference,
    string Status,
    string Origin,
    string Destination,
    DateTimeOffset DepartureUtc,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<TicketPassenger> Passengers,
    string QrPayload);

/// <summary>Returns the boarding ticket for a booking, with a QR payload encoding its reference.</summary>
public sealed class GetTicketHandler(IApplicationDbContext db)
{
    public async Task<TicketResult> HandleAsync(GetTicketQuery query, CancellationToken ct)
    {
        var booking = await db.Bookings
            .Include(b => b.Passengers)
            .FirstOrDefaultAsync(b => b.Id == query.BookingId, ct)
            ?? throw new NotFoundException("Booking", query.BookingId);

        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == booking.TripId, ct)
                   ?? throw new NotFoundException("Trip", booking.TripId);

        var passengers = booking.Passengers
            .OrderBy(p => p.SeatNumber)
            .Select(p => new TicketPassenger(p.FirstName, p.LastName, p.SeatNumber))
            .ToList();

        // QR payload: a compact token encoding the booking reference and trip.
        var qr = $"TPX|{booking.Reference}|{trip.Id}";

        return new TicketResult(
            booking.Id, booking.Reference, booking.Status.ToString(),
            trip.Origin, trip.Destination, trip.DepartureUtc,
            booking.TotalAmount, booking.Currency, passengers, qr);
    }
}
