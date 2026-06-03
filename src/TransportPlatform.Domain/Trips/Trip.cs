using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Trips;

/// <summary>
/// A scheduled journey on a specific bus. Seats are numbered 1..<see cref="SeatCount"/>.
/// Whether a seat is free is derived from active holds + confirmed assignments
/// (enforced by DB unique constraints), not stored as a mutable counter — this is what
/// makes overbooking impossible under concurrency.
/// </summary>
public sealed class Trip : AggregateRoot
{
    public Guid CompanyId { get; private set; }
    public Guid BusId { get; private set; }
    public string Origin { get; private set; } = null!;
    public string Destination { get; private set; } = null!;
    public DateTimeOffset DepartureUtc { get; private set; }
    public DateTimeOffset ArrivalUtc { get; private set; }
    public int SeatCount { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "SYP";
    public TripStatus Status { get; private set; } = TripStatus.Scheduled;

    public Money Fare => new(Price, Currency);

    private Trip() { } // EF

    public Trip(
        Guid companyId,
        Guid busId,
        string origin,
        string destination,
        DateTimeOffset departureUtc,
        DateTimeOffset arrivalUtc,
        int seatCount,
        Money fare)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("trip.company_required", "A trip must belong to a company.");
        if (busId == Guid.Empty)
            throw new DomainException("trip.bus_required", "A trip must have a bus.");
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(destination))
            throw new DomainException("trip.route_required", "Origin and destination are required.");
        if (string.Equals(origin.Trim(), destination.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new DomainException("trip.route_invalid", "Origin and destination must differ.");
        if (arrivalUtc <= departureUtc)
            throw new DomainException("trip.time_invalid", "Arrival must be after departure.");
        if (seatCount <= 0)
            throw new DomainException("trip.seats_invalid", "Seat count must be greater than zero.");

        CompanyId = companyId;
        BusId = busId;
        Origin = origin.Trim();
        Destination = destination.Trim();
        DepartureUtc = departureUtc;
        ArrivalUtc = arrivalUtc;
        SeatCount = seatCount;
        Price = fare.Amount;
        Currency = fare.Currency;
    }

    /// <summary>A seat number is valid only if it is within 1..SeatCount.</summary>
    public bool IsValidSeat(int seatNumber) => seatNumber >= 1 && seatNumber <= SeatCount;

    public void EnsureSeatsAreValid(IEnumerable<int> seatNumbers)
    {
        foreach (var seat in seatNumbers)
        {
            if (!IsValidSeat(seat))
                throw new DomainException("trip.seat_out_of_range",
                    $"Seat {seat} is not valid for this trip (1..{SeatCount}).");
        }
    }

    /// <summary>Bookings are only allowed while the trip is still scheduled and in the future.</summary>
    public void EnsureBookable(DateTimeOffset nowUtc)
    {
        if (Status != TripStatus.Scheduled)
            throw new DomainException("trip.not_bookable", "This trip is not open for booking.");
        if (DepartureUtc <= nowUtc)
            throw new DomainException("trip.departed", "This trip has already departed.");
    }

    public void Cancel() => Status = TripStatus.Cancelled;

    /// <summary>Edit route / times / fare. Only allowed while the trip is still Scheduled.</summary>
    public void Update(string origin, string destination, DateTimeOffset departureUtc, DateTimeOffset arrivalUtc, Money fare)
    {
        if (Status != TripStatus.Scheduled)
            throw new DomainException("trip.not_editable", "Only a scheduled trip can be edited.");
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(destination))
            throw new DomainException("trip.route_required", "Origin and destination are required.");
        if (string.Equals(origin.Trim(), destination.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new DomainException("trip.route_invalid", "Origin and destination must differ.");
        if (arrivalUtc <= departureUtc)
            throw new DomainException("trip.time_invalid", "Arrival must be after departure.");

        Origin = origin.Trim();
        Destination = destination.Trim();
        DepartureUtc = departureUtc;
        ArrivalUtc = arrivalUtc;
        Price = fare.Amount;
        Currency = fare.Currency;
    }

    /// <summary>Scheduled → InProgress (the bus has set off).</summary>
    public void Start()
    {
        if (Status != TripStatus.Scheduled)
            throw new DomainException("trip.not_startable", "Only a scheduled trip can be started.");
        Status = TripStatus.InProgress;
    }

    /// <summary>InProgress → Completed (the trip has finished).</summary>
    public void Complete()
    {
        if (Status != TripStatus.InProgress)
            throw new DomainException("trip.not_completable", "Only an in-progress trip can be completed.");
        Status = TripStatus.Completed;
    }
}
