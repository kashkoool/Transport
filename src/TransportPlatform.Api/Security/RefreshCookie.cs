namespace TransportPlatform.Api.Security;

/// <summary>
/// Carries the refresh token in an HttpOnly + Secure + SameSite=Strict cookie so a browser
/// SPA never has to store it in JavaScript-reachable storage. The access token stays in the
/// JSON body (kept in memory by the client); only the long-lived refresh token rides the
/// cookie. Scoped to <c>/api/auth</c> so it is never sent to booking/payment endpoints.
/// </summary>
internal static class RefreshCookie
{
    public const string Name = "rt";

    // The cookie is only ever needed by the refresh/logout endpoints, both under /api/auth.
    // Narrowing the path keeps it off every other request (smaller exposure, no CSRF surface
    // on the booking APIs).
    private const string Path = "/api/auth";

    /// <summary>
    /// Secure must be on in every real deployment; it is relaxed only in Development so the
    /// cookie still flows over plain-HTTP <c>ng serve</c> / TestServer. Production is HTTPS
    /// (and StartupGuards fails the boot if it is mis-set), so this never weakens prod.
    /// </summary>
    public static bool SecureFor(IHostEnvironment env) => !env.IsDevelopment();

    public static void Set(HttpResponse response, string token, DateTimeOffset expiresAtUtc, bool secure) =>
        response.Cookies.Append(Name, token, Options(secure, expiresAtUtc));

    public static void Delete(HttpResponse response, bool secure) =>
        response.Cookies.Delete(Name, Options(secure, expires: null));

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static CookieOptions Options(bool secure, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expires,
        IsEssential = true, // an auth cookie is exempt from consent gating
    };
}
