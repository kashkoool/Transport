using FluentValidation;
using TransportPlatform.Api.Security;
using TransportPlatform.Application.Reviews;

namespace TransportPlatform.Api.Endpoints;

public static class ReviewEndpoints
{
    public sealed record CreateReviewRequest(Guid BookingId, int Rating, string? Comment);

    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        // Submitting a review is login-required (you can only review your own travelled booking).
        app.MapPost("/api/reviews", async (
            CreateReviewRequest body, CreateReviewHandler handler,
            IValidator<CreateReviewCommand> validator, CancellationToken ct) =>
        {
            var command = new CreateReviewCommand(body.BookingId, body.Rating, body.Comment);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitPolicies.Sensitive)
        .WithTags("Reviews")
        .WithName("CreateReview")
        .WithSummary("Rate a trip you travelled on (1–5 + optional comment).");

        // Public: anyone browsing can see ratings (display name only — no PII).
        app.MapGet("/api/trips/{tripId:guid}/reviews", async (
            Guid tripId, int? page, int? limit, ListTripReviewsHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(tripId, page, limit, ct)))
        .RequireRateLimiting(RateLimitPolicies.PublicRead)
        .WithTags("Reviews")
        .WithName("ListTripReviews")
        .WithSummary("Public reviews + average rating for a trip.");

        app.MapGet("/api/companies/{companyId:guid}/reviews", async (
            Guid companyId, int? page, int? limit, ListCompanyReviewsHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(companyId, page, limit, ct)))
        .RequireRateLimiting(RateLimitPolicies.PublicRead)
        .WithTags("Reviews")
        .WithName("ListCompanyReviews")
        .WithSummary("Public reviews + average rating for a company.");

        return app;
    }
}
