namespace TransportPlatform.Application.Common;

/// <summary>
/// The single source of truth for which currencies the platform accepts (bound from the
/// <c>Currency</c> config section). Trip fares are validated against this allow-list; reports keep
/// revenue separated per currency rather than converting (no FX).
/// </summary>
public sealed class CurrencyOptions
{
    public const string SectionName = "Currency";

    /// <summary>Accepted ISO 4217 codes (upper-case). Defaults to SYP/USD/EUR.</summary>
    public IReadOnlyList<string> Supported { get; set; } = ["SYP", "USD", "EUR"];

    public bool IsSupported(string currency) =>
        !string.IsNullOrWhiteSpace(currency)
        && Supported.Any(c => string.Equals(c, currency.Trim(), StringComparison.OrdinalIgnoreCase));
}
