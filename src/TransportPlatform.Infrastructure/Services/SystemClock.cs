using TransportPlatform.Application.Common;

namespace TransportPlatform.Infrastructure.Services;

/// <summary>Production clock: returns the real UTC time.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
