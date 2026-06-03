using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.Application.Reports;

/// <summary>A revenue amount in one currency (revenue is reported per currency, never summed across).</summary>
public sealed record CurrencyAmount(string Currency, decimal Amount);

public sealed record AdminSystemSummary(
    int Companies, int ActiveCompanies, int Trips, int ConfirmedBookings, IReadOnlyList<CurrencyAmount> Revenue);

/// <summary>Platform-wide totals for the admin dashboard (SQL aggregation only). Revenue from
/// completed payments is grouped BY currency — mixing currencies into one sum would be meaningless.</summary>
public sealed class AdminSystemSummaryHandler(IApplicationDbContext db)
{
    public async Task<AdminSystemSummary> HandleAsync(CancellationToken ct)
    {
        var companies = await db.Companies.CountAsync(ct);
        var activeCompanies = await db.Companies.CountAsync(c => c.Status == CompanyStatus.Active, ct);
        var trips = await db.Trips.CountAsync(ct);
        var confirmedBookings = await db.Bookings.CountAsync(b => b.Status == BookingStatus.Confirmed, ct);

        var revenue = await db.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .GroupBy(p => p.Currency)
            .Select(g => new CurrencyAmount(g.Key, g.Sum(p => p.Amount)))
            .ToListAsync(ct);

        return new AdminSystemSummary(companies, activeCompanies, trips, confirmedBookings, revenue);
    }
}

public sealed record AdminCompanyReportRow(
    Guid CompanyId, string Name, string Status, int Trips, int ConfirmedBookings, IReadOnlyList<CurrencyAmount> Revenue);

/// <summary>
/// Per-company breakdown for the admin: trip count, confirmed bookings and revenue (by currency)
/// for each company. SQL aggregation; one bounded pass per metric, joined in memory by company id.
/// </summary>
public sealed class AdminCompanyReportHandler(IApplicationDbContext db)
{
    private const int MaxCompanies = 500;

    public async Task<IReadOnlyList<AdminCompanyReportRow>> HandleAsync(CancellationToken ct)
    {
        var companies = await db.Companies
            .OrderBy(c => c.Name)
            .Take(MaxCompanies)
            .Select(c => new { c.Id, c.Name, Status = c.Status.ToString() })
            .ToListAsync(ct);
        if (companies.Count == 0)
            return [];

        var ids = companies.Select(c => c.Id).ToList();

        var tripCounts = await db.Trips
            .Where(t => ids.Contains(t.CompanyId))
            .GroupBy(t => t.CompanyId)
            .Select(g => new { CompanyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Count, ct);

        var confirmedCounts = await db.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed && db.Trips.Any(t => t.Id == b.TripId && ids.Contains(t.CompanyId)))
            .Select(b => db.Trips.Where(t => t.Id == b.TripId).Select(t => t.CompanyId).First())
            .GroupBy(cid => cid)
            .Select(g => new { CompanyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Count, ct);

        // Completed-payment revenue per (company, currency).
        var revenue = await db.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Select(p => new
            {
                CompanyId = db.Bookings.Where(b => b.Id == p.BookingId)
                    .Select(b => db.Trips.Where(t => t.Id == b.TripId).Select(t => t.CompanyId).First())
                    .First(),
                p.Currency,
                p.Amount,
            })
            .Where(x => ids.Contains(x.CompanyId))
            .GroupBy(x => new { x.CompanyId, x.Currency })
            .Select(g => new { g.Key.CompanyId, g.Key.Currency, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        var revenueByCompany = revenue
            .GroupBy(x => x.CompanyId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CurrencyAmount>)g
                .Select(x => new CurrencyAmount(x.Currency, x.Amount)).ToList());

        return companies
            .Select(c => new AdminCompanyReportRow(
                c.Id, c.Name, c.Status,
                tripCounts.GetValueOrDefault(c.Id),
                confirmedCounts.GetValueOrDefault(c.Id),
                revenueByCompany.GetValueOrDefault(c.Id, [])))
            .ToList();
    }
}
