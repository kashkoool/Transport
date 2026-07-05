using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Fleet;

/// <summary>A physical vehicle owned by a company, with a fixed seat capacity + seat-map layout.</summary>
public sealed class Bus : AggregateRoot
{
    public const int DefaultSeatsPerRow = 4;
    public const int MaxSeatsPerRow = 6;

    public Guid CompanyId { get; private set; }
    public string BusNumber { get; private set; } = null!;
    public int SeatCount { get; private set; }
    public BusType Type { get; private set; }
    public string? Model { get; private set; }

    /// <summary>Vehicle registration / license plate, if recorded.</summary>
    public string? LicensePlate { get; private set; }

    /// <summary>When the bus's insurance expires, if tracked.</summary>
    public DateTimeOffset? InsuranceExpiryUtc { get; private set; }

    /// <summary>Seats per row, for rendering the seat map (e.g. 4 = a 2+2 coach).</summary>
    public int SeatsPerRow { get; private set; } = DefaultSeatsPerRow;

    /// <summary>The assigned driver, if any (drivers belong to the same company).</summary>
    public Guid? DriverId { get; private set; }

    private Bus() { } // EF

    public Bus(Guid companyId, string busNumber, int seatCount, BusType type, string? model = null,
        int seatsPerRow = DefaultSeatsPerRow, string? licensePlate = null, DateTimeOffset? insuranceExpiryUtc = null)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("bus.company_required", "A bus must belong to a company.");
        if (string.IsNullOrWhiteSpace(busNumber))
            throw new DomainException("bus.number_required", "Bus number is required.");

        CompanyId = companyId;
        BusNumber = busNumber.Trim();
        SetSeats(seatCount, seatsPerRow);
        Type = type;
        Model = model?.Trim();
        LicensePlate = string.IsNullOrWhiteSpace(licensePlate) ? null : licensePlate.Trim();
        InsuranceExpiryUtc = insuranceExpiryUtc;
    }

    /// <summary>Assign (or clear, when null) the driver for this bus.</summary>
    public void AssignDriver(Guid? driverId) => DriverId = driverId;

    /// <summary>Edit the bus details. (Bus number stays fixed — it's the fleet identifier.)</summary>
    public void Update(int seatCount, BusType type, string? model, int seatsPerRow = DefaultSeatsPerRow,
        string? licensePlate = null, DateTimeOffset? insuranceExpiryUtc = null)
    {
        SetSeats(seatCount, seatsPerRow);
        Type = type;
        Model = string.IsNullOrWhiteSpace(model) ? null : model.Trim();
        LicensePlate = string.IsNullOrWhiteSpace(licensePlate) ? null : licensePlate.Trim();
        InsuranceExpiryUtc = insuranceExpiryUtc;
    }

    private void SetSeats(int seatCount, int seatsPerRow)
    {
        if (seatCount <= 0)
            throw new DomainException("bus.seats_invalid", "Seat count must be greater than zero.");
        if (seatsPerRow is < 1 or > MaxSeatsPerRow)
            throw new DomainException("bus.layout_invalid", $"Seats per row must be 1..{MaxSeatsPerRow}.");
        SeatCount = seatCount;
        SeatsPerRow = seatsPerRow;
    }
}
