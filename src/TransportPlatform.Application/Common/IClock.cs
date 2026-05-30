namespace TransportPlatform.Application.Common;

/// <summary>
/// Abstraction over "now" so business rules and tests never read the wall clock directly.
/// Always UTC.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
