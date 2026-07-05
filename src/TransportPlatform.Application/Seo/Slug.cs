using System.Text;

namespace TransportPlatform.Application.Seo;

/// <summary>
/// URL-slug helpers for SEO routes and cities. Slugs are lowercased, trimmed, and have
/// runs of whitespace collapsed to a single hyphen. Route slugs join origin and destination
/// with the literal <c>-to-</c> separator (e.g. <c>damascus-to-aleppo</c>).
/// </summary>
public static class Slug
{
    /// <summary>Separator between origin and destination in a route slug.</summary>
    public const string RouteSeparator = "-to-";

    /// <summary>
    /// Maximum accepted slug length. Comfortably fits two ~120-char city names plus the
    /// separator, while rejecting absurdly long inputs before they reach the DB.
    /// </summary>
    public const int MaxLength = 260;

    /// <summary>City slug: lowercased, trimmed, whitespace runs → single hyphen.</summary>
    public static string City(string city) => Slugify(city);

    /// <summary>Route slug: <c>{origin}-to-{destination}</c>, each part slugified.</summary>
    public static string Route(string origin, string destination) =>
        $"{Slugify(origin)}{RouteSeparator}{Slugify(destination)}";

    /// <summary>
    /// Cheap syntactic gate before touching the DB: non-empty, within <see cref="MaxLength"/>,
    /// and only lowercase letters/digits/hyphens (the exact character set our slugs produce).
    /// Note this does not by itself validate a route (which must contain <c>-to-</c>).
    /// </summary>
    public static bool IsValid(string? slug)
    {
        if (string.IsNullOrEmpty(slug) || slug.Length > MaxLength)
            return false;

        return slug.All(ch => ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');
    }

    /// <summary>
    /// Resolve a route slug back to its (origin, destination) using the set of KNOWN routes,
    /// because either city may itself contain a hyphen and a blind split on <c>-to-</c> would be
    /// ambiguous. Returns the first known pair whose computed slug equals <paramref name="slug"/>.
    /// </summary>
    public static bool TryParseRoute(
        string slug,
        IEnumerable<(string Origin, string Destination)> knownRoutes,
        out string origin,
        out string destination)
    {
        origin = string.Empty;
        destination = string.Empty;

        if (!IsValid(slug) || !slug.Contains(RouteSeparator, StringComparison.Ordinal))
            return false;

        foreach (var (candidateOrigin, candidateDestination) in knownRoutes)
        {
            if (string.Equals(Route(candidateOrigin, candidateDestination), slug, StringComparison.Ordinal))
            {
                origin = candidateOrigin;
                destination = candidateDestination;
                return true;
            }
        }

        return false;
    }

    private static string Slugify(string value)
    {
        var trimmed = (value ?? string.Empty).Trim().ToLowerInvariant();
        var sb = new StringBuilder(trimmed.Length);
        var lastWasHyphen = false;

        foreach (var ch in trimmed)
        {
            if (char.IsWhiteSpace(ch) || ch == '-')
            {
                // Collapse any run of whitespace/hyphens into a single separating hyphen,
                // but never emit a leading one.
                if (!lastWasHyphen && sb.Length > 0)
                {
                    sb.Append('-');
                    lastWasHyphen = true;
                }
            }
            else
            {
                sb.Append(ch);
                lastWasHyphen = false;
            }
        }

        // Trim a possible trailing hyphen left by a value ending in whitespace/hyphen.
        if (sb.Length > 0 && sb[^1] == '-')
            sb.Length--;

        return sb.ToString();
    }
}
