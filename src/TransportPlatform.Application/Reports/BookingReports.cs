using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;

namespace TransportPlatform.Application.Reports;

public sealed record BookingReportRow(
    Guid BookingId, string Reference, string CustomerEmail, string Status,
    decimal TotalAmount, string Currency, DateTimeOffset CreatedAtUtc, int PassengerCount, string Gateway);

/// <summary>
/// Per-booking report for the company over a date range (by booking creation), with passenger
/// count and the payment gateway used. Company-scoped; computed with set/subquery aggregation
/// (no N+1). Used by the bookings report list + CSV/XLSX/PDF export.
/// </summary>
public sealed class VendorBookingReportHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<IReadOnlyList<BookingReportRow>> HandleAsync(VendorReportQuery query, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var from = query.From ?? clock.UtcNow.AddMonths(-1);
        var to = query.To ?? clock.UtcNow.AddMonths(1);

        var tripIds = db.Trips.Where(t => t.CompanyId == companyId).Select(t => t.Id);

        return await db.Bookings
            .Where(b => tripIds.Contains(b.TripId) && b.CreatedAtUtc >= from && b.CreatedAtUtc < to)
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => new BookingReportRow(
                b.Id, b.Reference, b.CustomerEmail, b.Status.ToString(),
                b.TotalAmount, b.Currency, b.CreatedAtUtc,
                b.Passengers.Count,
                db.Payments.Where(p => p.BookingId == b.Id).Select(p => p.Gateway).FirstOrDefault() ?? "—"))
            .ToListAsync(ct);
    }
}

/// <summary>Column headers + formula-injection-safe CSV for the per-booking report.</summary>
public static class BookingReportCsv
{
    public static readonly IReadOnlyList<string> Headers =
        ["Reference", "Customer", "Status", "Total", "Currency", "CreatedAtUtc", "Passengers", "Gateway"];

    public static IReadOnlyList<string> ToCells(BookingReportRow r) =>
    [
        r.Reference, r.CustomerEmail, r.Status, CsvWriter.Cell(r.TotalAmount), r.Currency,
        CsvWriter.Cell(r.CreatedAtUtc), CsvWriter.Cell(r.PassengerCount), r.Gateway,
    ];

    public static string Build(IReadOnlyList<BookingReportRow> rows) =>
        CsvWriter.Build(Headers, rows.Select(ToCells));
}

public sealed record EmployeeReportRow(
    Guid StaffId, string Email, string FullName, int Bookings, decimal Revenue, string Currency);

/// <summary>
/// Per-employee activity: how many bookings each staff member created at the desk and the revenue
/// from them, over a date range. Only the company's own staff are considered (online bookings
/// created by customers are excluded). SQL grouping on CreatedBy; names resolved from the roster.
/// </summary>
public sealed class VendorEmployeeReportHandler(IApplicationDbContext db, IIdentityService identity, ICurrentUser currentUser, IClock clock)
{
    public async Task<IReadOnlyList<EmployeeReportRow>> HandleAsync(VendorReportQuery query, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var from = query.From ?? clock.UtcNow.AddMonths(-1);
        var to = query.To ?? clock.UtcNow.AddMonths(1);

        // The company's staff roster (bounded), keyed by their user-id string (= Booking.CreatedBy).
        var staff = await identity.ListStaffAsync(companyId, 0, int.MaxValue, null, ct);
        if (staff.Count == 0)
            return [];
        var staffById = staff.ToDictionary(s => s.Id.ToString());
        var staffIds = staffById.Keys.ToList();

        var tripIds = db.Trips.Where(t => t.CompanyId == companyId).Select(t => t.Id);

        // Group desk bookings by (operator, currency) so revenue is never summed across currencies
        // (a company may sell in SYP/USD/EUR). One row per staff member per currency they sold in.
        var grouped = await db.Bookings
            .Where(b => tripIds.Contains(b.TripId)
                        && b.CreatedBy != null && staffIds.Contains(b.CreatedBy)
                        && b.Status != BookingStatus.Cancelled
                        && b.CreatedAtUtc >= from && b.CreatedAtUtc < to)
            .GroupBy(b => new { b.CreatedBy, b.Currency })
            .Select(g => new { g.Key.CreatedBy, g.Key.Currency, Bookings = g.Count(), Revenue = g.Sum(b => b.TotalAmount) })
            .ToListAsync(ct);

        return grouped
            .Where(g => g.CreatedBy != null && staffById.ContainsKey(g.CreatedBy))
            .Select(g =>
            {
                var s = staffById[g.CreatedBy!];
                return new EmployeeReportRow(s.Id, s.Email, s.FullName, g.Bookings, g.Revenue, g.Currency);
            })
            .OrderByDescending(r => r.Revenue)
            .ToList();
    }
}
