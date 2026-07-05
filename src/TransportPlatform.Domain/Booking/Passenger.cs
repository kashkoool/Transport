using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Bookings;

/// <summary>A traveller on a booking, occupying exactly one seat. May carry an ID document.</summary>
public sealed class Passenger : Entity
{
    public const int MaxDocumentLength = 60;

    public Guid BookingId { get; private set; }
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public int SeatNumber { get; private set; }

    /// <summary>Optional travel-document type (e.g. "NationalId", "Passport") + number.</summary>
    public string? DocumentType { get; private set; }
    public string? DocumentNumber { get; private set; }

    private Passenger() { } // EF

    public Passenger(string firstName, string lastName, int seatNumber, string? documentType = null, string? documentNumber = null)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("passenger.name_required", "Passenger name is required.");
        if (seatNumber < 1)
            throw new DomainException("passenger.seat_invalid", "Seat number must be positive.");

        var docType = documentType?.Trim();
        var docNumber = documentNumber?.Trim();
        if (docType is { Length: > MaxDocumentLength } || docNumber is { Length: > MaxDocumentLength })
            throw new DomainException("passenger.document_too_long",
                $"Document fields cannot exceed {MaxDocumentLength} characters.");

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        SeatNumber = seatNumber;
        DocumentType = string.IsNullOrWhiteSpace(docType) ? null : docType;
        DocumentNumber = string.IsNullOrWhiteSpace(docNumber) ? null : docNumber;
    }

    /// <summary>Move this passenger to a different seat (driven by the Booking aggregate root).</summary>
    internal void MoveToSeat(int seatNumber)
    {
        if (seatNumber < 1)
            throw new DomainException("passenger.seat_invalid", "Seat number must be positive.");
        SeatNumber = seatNumber;
    }
}
