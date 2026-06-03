using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Reviews;

public sealed record ReviewSummary(double AverageRating, int Count, IReadOnlyList<ReviewDto> Reviews);

/// <summary>Public trip reviews: average rating + a page of recent reviews (display name only).</summary>
public sealed class ListTripReviewsHandler(IApplicationDbContext db)
{
    public Task<ReviewSummary> HandleAsync(Guid tripId, int? page, int? limit, CancellationToken ct) =>
        SummariseAsync(db.Reviews.Where(r => r.TripId == tripId), page, limit, ct);

    internal static async Task<ReviewSummary> SummariseAsync(
        IQueryable<Domain.Reviews.Review> source, int? page, int? limit, CancellationToken ct)
    {
        var pageReq = new PageRequest(page, limit);
        var count = await source.CountAsync(ct);
        var average = count == 0 ? 0 : Math.Round(await source.AverageAsync(r => (double)r.Rating, ct), 2);
        var reviews = await source
            .OrderByDescending(r => r.CreatedAtUtc)
            .ThenByDescending(r => r.Id)
            .Skip(pageReq.Skip)
            .Take(pageReq.Limit)
            .Select(r => new ReviewDto(r.Id, r.Rating, r.Comment, r.DisplayName, r.CreatedAtUtc))
            .ToListAsync(ct);
        return new ReviewSummary(average, count, reviews);
    }
}

/// <summary>Public company reviews: aggregate rating across all the company's trips.</summary>
public sealed class ListCompanyReviewsHandler(IApplicationDbContext db)
{
    public Task<ReviewSummary> HandleAsync(Guid companyId, int? page, int? limit, CancellationToken ct) =>
        ListTripReviewsHandler.SummariseAsync(db.Reviews.Where(r => r.CompanyId == companyId), page, limit, ct);
}
