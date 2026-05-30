namespace TransportPlatform.Domain.Trips;

/// <summary>Lifecycle of a scheduled trip.</summary>
public enum TripStatus
{
    Scheduled = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
}
