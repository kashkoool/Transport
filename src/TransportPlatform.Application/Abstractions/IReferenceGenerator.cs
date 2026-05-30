namespace TransportPlatform.Application.Abstractions;

/// <summary>Generates human-friendly, unique booking references (e.g. "BK-7F3K9Q").</summary>
public interface IReferenceGenerator
{
    string NewBookingReference();
}
