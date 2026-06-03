using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.Application.Reports;

public sealed record VendorReportQuery(DateTimeOffset? From, DateTimeOffset? To);

public sealed record VendorReportSummary(
    DateTimeOffset From, DateTimeOffset To, int Trips, int ConfirmedBookings,
    int SeatsSold, int SeatsOffered, decimal Revenue, string Currency, double OccupancyPct);

/// <summary>
/// Company-scoped financial/occupancy summary over a date range (by trip departure). All numbers
/// are computed with SQL aggregation (no in-memory summing). Revenue = completed payments for the
/// company's bookings in range.
/// </summary>
public sealed class VendorReportSummaryHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<VendorReportSummary> HandleAsync(VendorReportQuery query, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var from = query.From ?? clock.UtcNow.AddMonths(-1);
        var to = query.To ?? clock.UtcNow.AddMonths(1);

        var trips = db.Trips.Where(t => t.CompanyId == companyId && t.DepartureUtc >= from && t.DepartureUtc < to);
        var tripIds = trips.Select(t => t.Id);

        var tripCount = await trips.CountAsync(ct);
        var seatsOffered = await trips.SumAsync(t => (int?)t.SeatCount, ct) ?? 0;
        var currency = await trips.Select(t => t.Currency).FirstOrDefaultAsync(ct) ?? "SYP";

        var bookingIds = db.Bookings.Where(b => tripIds.Contains(b.TripId));
        var confirmedBookings = await bookingIds.CountAsync(b => b.Status == BookingStatus.Confirmed, ct);
        var seatsSold = await db.SeatAssignments.CountAsync(a => tripIds.Contains(a.TripId), ct);

        var revenue = await db.Payments
            .Where(p => p.Status == PaymentStatus.Completed && bookingIds.Select(b => b.Id).Contains(p.BookingId))
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var occupancy = seatsOffered > 0 ? Math.Round(seatsSold * 100.0 / seatsOffered, 1) : 0;
        return new VendorReportSummary(from, to, tripCount, confirmedBookings, seatsSold, seatsOffered, revenue, currency, occupancy);
    }
}

public sealed record TripReportRow(
    Guid TripId, string Origin, string Destination, DateTimeOffset DepartureUtc,
    int SeatCount, int SeatsSold, decimal Revenue, string Currency, string Status);

/// <summary>Per-trip occupancy + revenue rows for the company (used by the list + CSV export).</summary>
public sealed class VendorTripReportHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<IReadOnlyList<TripReportRow>> HandleAsync(VendorReportQuery query, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var from = query.From ?? clock.UtcNow.AddMonths(-1);
        var to = query.To ?? clock.UtcNow.AddMonths(1);

        // Seats sold per trip (single grouped query — no N+1).
        var trips = await db.Trips
            .Where(t => t.CompanyId == companyId && t.DepartureUtc >= from && t.DepartureUtc < to)
            .OrderByDescending(t => t.DepartureUtc)
            .Select(t => new
            {
                t.Id, t.Origin, t.Destination, t.DepartureUtc, t.SeatCount, t.Currency,
                Status = t.Status.ToString(),
                SeatsSold = db.SeatAssignments.Count(a => a.TripId == t.Id),
                Revenue = db.Payments
                    .Where(p => p.Status == PaymentStatus.Completed
                        && db.Bookings.Any(b => b.Id == p.BookingId && b.TripId == t.Id))
                    .Sum(p => (decimal?)p.Amount) ?? 0m,
            })
            .ToListAsync(ct);

        return trips
            .Select(t => new TripReportRow(t.Id, t.Origin, t.Destination, t.DepartureUtc,
                t.SeatCount, t.SeatsSold, t.Revenue, t.Currency, t.Status))
            .ToList();
    }

    /// <summary>CSV (formula-injection-safe) of the per-trip report.</summary>
    public async Task<string> ExportCsvAsync(VendorReportQuery query, CancellationToken ct)
    {
        var rows = await HandleAsync(query, ct);
        return CsvWriter.Build(
            ["TripId", "Origin", "Destination", "DepartureUtc", "Seats", "SeatsSold", "Revenue", "Currency", "Status"],
            rows.Select(r => new[]
            {
                r.TripId.ToString(), r.Origin, r.Destination, CsvWriter.Cell(r.DepartureUtc),
                CsvWriter.Cell(r.SeatCount), CsvWriter.Cell(r.SeatsSold), CsvWriter.Cell(r.Revenue), r.Currency, r.Status,
            }));
    }
}
