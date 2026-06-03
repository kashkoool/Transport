using TransportPlatform.Application.Reports;

namespace TransportPlatform.Application.Abstractions;

/// <summary>
/// Renders report rows to binary formats (XLSX, PDF). Implemented in Infrastructure (ClosedXML /
/// QuestPDF) so the use-cases stay free of the document libraries. CSV is dependency-free and lives
/// in the Application layer (<see cref="Reports.TripReportCsv"/>).
/// </summary>
public interface IReportExporter
{
    byte[] TripsToXlsx(IReadOnlyList<TripReportRow> rows);
    byte[] TripsToPdf(IReadOnlyList<TripReportRow> rows);
}
