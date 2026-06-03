using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Reviews;

namespace TransportPlatform.Application.Reviews;

public sealed record CreateReviewCommand(Guid BookingId, int Rating, string? Comment);

public sealed class CreateReviewValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(Review.MinRating, Review.MaxRating);
        RuleFor(x => x.Comment).MaximumLength(Review.MaxCommentLength);
    }
}

/// <summary>Public-safe read model for a review — display name only, never the customer's email.</summary>
public sealed record ReviewDto(Guid Id, int Rating, string? Comment, string DisplayName, DateTimeOffset CreatedAtUtc);

/// <summary>
/// A customer reviews a trip they actually travelled on: the booking must be theirs + confirmed,
/// the trip must have departed, and one review per booking. The public display name is first name +
/// last initial (taken from the booking's lead passenger).
/// </summary>
public sealed class CreateReviewHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<ReviewDto> HandleAsync(CreateReviewCommand command, CancellationToken ct)
    {
        var email = currentUser.RequireEmail();
        var booking = await db.Bookings.Include(b => b.Passengers)
            .FirstOrDefaultAsync(b => b.Id == command.BookingId && b.CustomerEmail == email, ct)
            ?? throw new NotFoundException("Booking", command.BookingId);

        if (booking.Status != BookingStatus.Confirmed)
            throw new ConflictException("review.not_eligible", "You can only review a confirmed booking.");

        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == booking.TripId, ct)
            ?? throw new NotFoundException("Trip", booking.TripId);
        if (trip.DepartureUtc > clock.UtcNow)
            throw new ConflictException("review.too_early", "You can review a trip only after it has departed.");

        if (await db.Reviews.AnyAsync(r => r.BookingId == booking.Id, ct))
            throw new ConflictException("review.exists", "You've already reviewed this booking.");

        var review = new Review(trip.Id, trip.CompanyId, booking.Id, email,
            DisplayNameFrom(booking), command.Rating, command.Comment);
        db.Reviews.Add(review);
        await db.SaveChangesAsync(ct);

        return new ReviewDto(review.Id, review.Rating, review.Comment, review.DisplayName, review.CreatedAtUtc);
    }

    private static string DisplayNameFrom(Booking booking)
    {
        var passenger = booking.Passengers.FirstOrDefault();
        if (passenger is null)
            return "Traveller";
        var lastInitial = string.IsNullOrWhiteSpace(passenger.LastName) ? string.Empty : $" {passenger.LastName.Trim()[..1]}.";
        return $"{passenger.FirstName.Trim()}{lastInitial}";
    }
}
