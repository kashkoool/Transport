using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Reviews;

/// <summary>
/// A customer's rating + optional comment for a trip they actually travelled on. The public-facing
/// identity is a <see cref="DisplayName"/> (first name + last initial) — the customer's email is
/// kept only for ownership/dedup and is never exposed publicly.
/// </summary>
public sealed class Review : AggregateRoot
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int MaxCommentLength = 1000;

    public Guid TripId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid BookingId { get; private set; }
    public string CustomerEmail { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public int Rating { get; private set; }
    public string? Comment { get; private set; }

    private Review() { } // EF

    public Review(Guid tripId, Guid companyId, Guid bookingId, string customerEmail, string displayName, int rating, string? comment)
    {
        if (tripId == Guid.Empty || companyId == Guid.Empty || bookingId == Guid.Empty)
            throw new DomainException("review.refs_required", "Trip, company and booking are required.");
        if (rating is < MinRating or > MaxRating)
            throw new DomainException("review.rating_invalid", $"Rating must be between {MinRating} and {MaxRating}.");

        var trimmedComment = comment?.Trim();
        if (trimmedComment is { Length: > MaxCommentLength })
            throw new DomainException("review.comment_too_long", $"Comment cannot exceed {MaxCommentLength} characters.");

        TripId = tripId;
        CompanyId = companyId;
        BookingId = bookingId;
        CustomerEmail = customerEmail.Trim().ToLowerInvariant();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Traveller" : displayName.Trim();
        Rating = rating;
        Comment = string.IsNullOrWhiteSpace(trimmedComment) ? null : trimmedComment;
    }
}
