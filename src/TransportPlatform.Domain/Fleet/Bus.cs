using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Fleet;

/// <summary>A physical vehicle owned by a company, with a fixed seat capacity.</summary>
public sealed class Bus : AggregateRoot
{
    public Guid CompanyId { get; private set; }
    public string BusNumber { get; private set; } = null!;
    public int SeatCount { get; private set; }
    public BusType Type { get; private set; }
    public string? Model { get; private set; }

    /// <summary>The assigned driver, if any (drivers belong to the same company).</summary>
    public Guid? DriverId { get; private set; }

    private Bus() { } // EF

    public Bus(Guid companyId, string busNumber, int seatCount, BusType type, string? model = null)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("bus.company_required", "A bus must belong to a company.");
        if (string.IsNullOrWhiteSpace(busNumber))
            throw new DomainException("bus.number_required", "Bus number is required.");
        if (seatCount <= 0)
            throw new DomainException("bus.seats_invalid", "Seat count must be greater than zero.");

        CompanyId = companyId;
        BusNumber = busNumber.Trim();
        SeatCount = seatCount;
        Type = type;
        Model = model?.Trim();
    }

    /// <summary>Assign (or clear, when null) the driver for this bus.</summary>
    public void AssignDriver(Guid? driverId) => DriverId = driverId;

    /// <summary>Edit the bus details. (Bus number stays fixed — it's the fleet identifier.)</summary>
    public void Update(int seatCount, BusType type, string? model)
    {
        if (seatCount <= 0)
            throw new DomainException("bus.seats_invalid", "Seat count must be greater than zero.");
        SeatCount = seatCount;
        Type = type;
        Model = string.IsNullOrWhiteSpace(model) ? null : model.Trim();
    }
}
