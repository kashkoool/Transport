namespace TransportPlatform.Application.Common;

/// <summary>A requested resource does not exist. Maps to HTTP 404.</summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.")
{
    /// <summary>Stable, machine-readable error code.</summary>
    public string Code { get; } = "not_found";
}

/// <summary>
/// The request conflicts with current state (e.g. a seat was just taken). Maps to HTTP 409.
/// </summary>
public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
