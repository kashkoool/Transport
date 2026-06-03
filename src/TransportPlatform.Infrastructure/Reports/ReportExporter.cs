using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Reports;

namespace TransportPlatform.Infrastructure.Reports;

/// <summary>
/// Renders the per-trip report to XLSX (ClosedXML) and PDF (QuestPDF). XLSX cells are written as
/// typed values (numbers/dates as numbers/dates, text as inline strings — never formulas).
/// </summary>
public sealed class ReportExporter : IReportExporter
{
    static ReportExporter() => QuestPDF.Settings.License = LicenseType.Community; // QuestPDF community licence

    public byte[] TripsToXlsx(IReadOnlyList<TripReportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Trips");

        var headers = TripReportCsv.Headers;
        for (var c = 0; c < headers.Count; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        ws.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.TripId.ToString();
            ws.Cell(row, 2).Value = r.Origin;
            ws.Cell(row, 3).Value = r.Destination;
            ws.Cell(row, 4).Value = r.DepartureUtc.UtcDateTime;
            ws.Cell(row, 5).Value = r.SeatCount;
            ws.Cell(row, 6).Value = r.SeatsSold;
            ws.Cell(row, 7).Value = (double)r.Revenue;
            ws.Cell(row, 8).Value = r.Currency;
            ws.Cell(row, 9).Value = r.Status;
            row++;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] TripsToPdf(IReadOnlyList<TripReportRow> rows)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Text("Trips report").FontSize(16).Bold();

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in TripReportCsv.Headers)
                            columns.RelativeColumn();
                    });

                    foreach (var header in TripReportCsv.Headers)
                        table.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(header).Bold();

                    foreach (var r in rows)
                        foreach (var cell in TripReportCsv.ToCells(r))
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cell);
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ToXlsx(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet(sheetName);

        for (var c = 0; c < headers.Count; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        ws.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var cells in rows)
        {
            for (var c = 0; c < cells.Count; c++)
                ws.Cell(row, c + 1).SetValue(cells[c]); // inline strings only — never formulas
            row++;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ToPdf(string title, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Text(title).FontSize(16).Bold();

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in headers)
                            columns.RelativeColumn();
                    });

                    foreach (var header in headers)
                        table.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(header).Bold();

                    foreach (var cells in rows)
                        foreach (var cell in cells)
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cell);
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }
}
