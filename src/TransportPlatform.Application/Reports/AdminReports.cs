using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.Application.Reports;

public sealed record AdminSystemSummary(
    int Companies, int ActiveCompanies, int Trips, int ConfirmedBookings, decimal Revenue);

/// <summary>Platform-wide totals for the admin dashboard (SQL aggregation only).</summary>
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
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        return new AdminSystemSummary(companies, activeCompanies, trips, confirmedBookings, revenue);
    }
}
