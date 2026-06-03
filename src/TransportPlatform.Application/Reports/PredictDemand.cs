using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Reports;

public sealed record PredictDemandQuery(string Origin, string Destination, DateOnly Date);

public sealed record DemandPrediction(
    string Origin, string Destination, DateOnly Date,
    int PredictedBookings, string Confidence, int SampleSize);

public sealed class PredictDemandValidator : AbstractValidator<PredictDemandQuery>
{
    public PredictDemandValidator()
    {
        RuleFor(x => x.Origin).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(120);
    }
}

/// <summary>
/// Statistical demand forecast for a route/date from the company's own last-90-days history:
/// average seats sold on the target day-of-week, adjusted by a recent-vs-earlier trend and a
/// seasonal factor (weekend / summer / Dec-Jan). Confidence scales with the sample size. No ML.
/// </summary>
public sealed class PredictDemandHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
{
    private const int HistoryDays = 90;

    public async Task<DemandPrediction> HandleAsync(PredictDemandQuery query, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var now = clock.UtcNow;
        var windowStart = now.AddDays(-HistoryDays);
        var origin = query.Origin.Trim().ToLowerInvariant();
        var destination = query.Destination.Trim().ToLowerInvariant();

        // Per past trip on this route: how many seats were actually sold. Bounded (≤ trips in 90d).
#pragma warning disable CA1304, CA1311, CA1862 // ToLower() is translated to SQL lower(); no culture involved
        var history = await db.Trips
            .Where(t => t.CompanyId == companyId
                        && t.DepartureUtc >= windowStart && t.DepartureUtc < now
                        && t.Origin.ToLower() == origin && t.Destination.ToLower() == destination)
            .Select(t => new { t.DepartureUtc, Demand = db.SeatAssignments.Count(a => a.TripId == t.Id) })
            .ToListAsync(ct);
#pragma warning restore CA1304, CA1311, CA1862

        var sampleSize = history.Count;
        if (sampleSize == 0)
            return new DemandPrediction(query.Origin, query.Destination, query.Date, 0, "low", 0);

        // Baseline: average demand on the target day-of-week (fall back to overall average).
        var dow = query.Date.DayOfWeek;
        var sameDow = history.Where(h => h.DepartureUtc.DayOfWeek == dow).Select(h => h.Demand).ToList();
        var baseline = sameDow.Count > 0 ? sameDow.Average() : history.Average(h => h.Demand);

        // Trend: recent 30 days vs the prior 60 days.
        var recentCut = now.AddDays(-30);
        var recent = history.Where(h => h.DepartureUtc >= recentCut).Select(h => h.Demand).ToList();
        var earlier = history.Where(h => h.DepartureUtc < recentCut).Select(h => h.Demand).ToList();
        var trend = recent.Count > 0 && earlier.Count > 0 && earlier.Average() > 0
            ? Math.Clamp(recent.Average() / earlier.Average(), 0.5, 2.0)
            : 1.0;

        var predicted = (int)Math.Round(baseline * trend * SeasonalFactor(query.Date));
        var confidence = sampleSize >= 12 ? "high" : sampleSize >= 4 ? "medium" : "low";
        return new DemandPrediction(query.Origin, query.Destination, query.Date, Math.Max(0, predicted), confidence, sampleSize);
    }

    private static double SeasonalFactor(DateOnly date)
    {
        var factor = 1.0;
        if (date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday) factor *= 1.2; // weekend travel
        if (date.Month is 6 or 7 or 8) factor *= 1.1;  // summer
        if (date.Month is 12 or 1) factor *= 1.2;       // holidays
        return factor;
    }
}
