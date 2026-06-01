using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// Exercises the JWT auth flow end-to-end against the real API + Postgres: registration,
/// login, the authenticated /me endpoint, refresh-token rotation, and reuse detection.
/// </summary>
public sealed class AuthFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "Str0ng!Passw0rd";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Register_login_me_refresh_and_reuse_detection()
    {
        var client = factory.CreateClient();
        var email = $"u{Guid.NewGuid():N}@example.com";

        // 1. Register → tokens issued.
        var registerResp = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = Password, fullName = "Test User" });
        registerResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var registered = await registerResp.Content.ReadFromJsonAsync<AuthDto>(Json);
        registered!.AccessToken.Should().NotBeNullOrEmpty();
        registered.RefreshToken.Should().NotBeNullOrEmpty();

        // 2. Login with the same credentials → succeeds.
        var loginResp = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = Password });
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var loggedIn = await loginResp.Content.ReadFromJsonAsync<AuthDto>(Json);
        loggedIn!.AccessToken.Should().NotBeNullOrEmpty();

        // 3. GET /me with a Bearer token → returns the caller's email.
        var me = await GetMeAsync(client, registered.AccessToken);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var meBody = await me.Content.ReadAsStringAsync();
        meBody.Should().Contain(email);

        // 4. GET /me without a token → unauthorized.
        var anonymous = await client.GetAsync("/api/auth/me");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 5. Refresh rotates the refresh token (a different value is returned).
        var refreshResp = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = registered.RefreshToken });
        refreshResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshed = await refreshResp.Content.ReadFromJsonAsync<AuthDto>(Json);
        refreshed!.RefreshToken.Should().NotBeNullOrEmpty();
        refreshed.RefreshToken.Should().NotBe(registered.RefreshToken);

        // 6. Replaying the now-rotated refresh token → reuse detected, unauthorized.
        var reuseResp = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = registered.RefreshToken });
        reuseResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_works_from_the_HttpOnly_cookie_alone_and_rotation_is_detected()
    {
        // Manage cookies by hand so we can assert the Set-Cookie attributes and deliberately
        // replay a stale cookie (the auto cookie container would silently overwrite it).
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var email = $"u{Guid.NewGuid():N}@example.com";

        // 1. Register → the refresh token is delivered as an HttpOnly cookie.
        var registerResp = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = Password, fullName = "Cookie User" });
        registerResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var setCookie = registerResp.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("rt=", StringComparison.Ordinal));
        setCookie.Should().Contain("httponly", "the refresh token must not be reachable from JavaScript");
        setCookie.Should().Contain("samesite=strict");
        setCookie.Should().Contain("path=/api/auth");
        var originalCookie = CookiePair(setCookie);

        // 2. Refresh with ONLY the cookie (empty JSON body, no token field) → succeeds and
        //    rotates: a fresh rt cookie comes back.
        var refreshResp = await PostWithCookieAsync(client, "/api/auth/refresh", originalCookie);
        refreshResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotatedCookie = CookiePair(refreshResp.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("rt=", StringComparison.Ordinal)));
        rotatedCookie.Should().NotBe(originalCookie, "rotation must issue a new refresh token");

        // 3. Replaying the now-rotated original cookie → reuse detected, unauthorized.
        var reuseResp = await PostWithCookieAsync(client, "/api/auth/refresh", originalCookie);
        reuseResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 4. Logout clears the cookie (Set-Cookie with an expired/empty rt).
        var logoutResp = await PostWithCookieAsync(client, "/api/auth/logout", rotatedCookie);
        logoutResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        logoutResp.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith("rt=", StringComparison.Ordinal));
    }

    private static Task<HttpResponseMessage> PostWithCookieAsync(HttpClient client, string path, string rtCookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            // Send an empty JSON object so model binding for the optional body succeeds.
            Content = JsonContent.Create(new { }),
        };
        request.Headers.Add("Cookie", rtCookie);
        return client.SendAsync(request);
    }

    /// <summary>Extract just the `name=value` pair from a Set-Cookie header (drops attributes).</summary>
    private static string CookiePair(string setCookieHeader) => setCookieHeader.Split(';', 2)[0];

    private static Task<HttpResponseMessage> GetMeAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    private sealed record AuthDto(string AccessToken, string RefreshToken, string Email);
}
