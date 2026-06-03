using System.Globalization;
using System.Text;

namespace TransportPlatform.Application.Common;

/// <summary>
/// Minimal, dependency-free CSV builder. RFC-4180 quoting PLUS formula-injection defense: a field
/// beginning with = + - @ (or a tab/CR) is prefixed with a single quote so spreadsheet apps don't
/// execute it as a formula (per the security skill).
/// </summary>
public static class CsvWriter
{
    public static string Build(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', header.Select(Escape)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(',', row.Select(Escape)));
        return sb.ToString();
    }

    public static string Cell(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    public static string Cell(int value) => value.ToString(CultureInfo.InvariantCulture);
    public static string Cell(DateTimeOffset value) => value.ToString("u", CultureInfo.InvariantCulture);

    private static string Escape(string? field)
    {
        var value = field ?? string.Empty;

        // Formula-injection guard: neutralize a leading active character.
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;

        // RFC-4180 quoting when the value contains a comma, quote, or newline.
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            value = "\"" + value.Replace("\"", "\"\"") + "\"";

        return value;
    }
}
