namespace TransportPlatform.Domain.Common;

/// <summary>
/// Thrown when a domain invariant would be violated. The application layer
/// translates these into a safe 4xx response — they are expected business errors,
/// not bugs.
/// </summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    /// <summary>Stable, machine-readable error code (e.g. "seat.already_taken").</summary>
    public string Code { get; } = code;
}
