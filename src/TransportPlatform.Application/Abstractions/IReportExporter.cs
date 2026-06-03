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

    /// <summary>Generic XLSX from string headers + rows (used by booking/employee/company reports).</summary>
    byte[] ToXlsx(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows);

    /// <summary>Generic PDF table from string headers + rows.</summary>
    byte[] ToPdf(string title, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows);
}
