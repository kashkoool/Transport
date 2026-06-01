namespace TransportPlatform.Application.Common;

/// <summary>A page of results plus the totals a client needs to paginate.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Data, int Total, int Page, int Limit)
{
    public int TotalPages => Limit <= 0 ? 0 : (int)Math.Ceiling((double)Total / Limit);
}

/// <summary>
/// Normalizes raw <c>?page=&amp;limit=</c> query input: page defaults to 1, limit defaults to
/// 20 and is hard-capped at 100 so a client can never request an unbounded result set.
/// </summary>
public readonly record struct PageRequest
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public int Page { get; }
    public int Limit { get; }

    public PageRequest(int? page, int? limit)
    {
        Page = page is > 0 ? page.Value : 1;
        Limit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
    }

    public int Skip => (Page - 1) * Limit;
}
