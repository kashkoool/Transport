using TransportPlatform.Domain.Trips;

namespace TransportPlatform.Application.Trips;

/// <summary>Read model for a scheduled trip (vendor + management views).</summary>
public sealed record TripDto(
    Guid Id,
    Guid BusId,
    string Origin,
    string Destination,
    DateTimeOffset DepartureUtc,
    DateTimeOffset ArrivalUtc,
    int SeatCount,
    decimal Price,
    string Currency,
    string Status)
{
    public static TripDto From(Trip t) =>
        new(t.Id, t.BusId, t.Origin, t.Destination, t.DepartureUtc, t.ArrivalUtc,
            t.SeatCount, t.Price, t.Currency, t.Status.ToString());
}
