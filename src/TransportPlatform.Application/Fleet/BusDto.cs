using TransportPlatform.Domain.Fleet;

namespace TransportPlatform.Application.Fleet;

/// <summary>Read model for a bus in a company's fleet.</summary>
public sealed record BusDto(Guid Id, string BusNumber, int SeatCount, string Type, string? Model, Guid? DriverId, int SeatsPerRow)
{
    public static BusDto From(Bus b) =>
        new(b.Id, b.BusNumber, b.SeatCount, b.Type.ToString(), b.Model, b.DriverId, b.SeatsPerRow);
}
