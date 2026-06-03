using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Trips;

/// <summary>
/// An intermediate waypoint on a trip's route, ordered by <see cref="Sequence"/> between
/// the trip's origin and destination. Times are optional (a schedule may only list the stop name).
/// </summary>
public sealed class TripStop : Entity
{
    public const int MaxNameLength = 120;

    public Guid TripId { get; private set; }
    public int Sequence { get; private set; }
    public string Name { get; private set; } = null!;
    public DateTimeOffset? ArrivalUtc { get; private set; }
    public DateTimeOffset? DepartureUtc { get; private set; }

    private TripStop() { } // EF

    public TripStop(int sequence, string name, DateTimeOffset? arrivalUtc = null, DateTimeOffset? departureUtc = null)
    {
        if (sequence < 1)
            throw new DomainException("trip_stop.sequence_invalid", "Stop sequence must be positive.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("trip_stop.name_required", "A stop name is required.");
        if (name.Trim().Length > MaxNameLength)
            throw new DomainException("trip_stop.name_too_long", $"Stop name cannot exceed {MaxNameLength} characters.");
        if (arrivalUtc is not null && departureUtc is not null && departureUtc < arrivalUtc)
            throw new DomainException("trip_stop.time_invalid", "Stop departure cannot be before its arrival.");

        Sequence = sequence;
        Name = name.Trim();
        ArrivalUtc = arrivalUtc;
        DepartureUtc = departureUtc;
    }
}
