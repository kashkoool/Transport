using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

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

    private static Task<HttpResponseMessage> GetMeAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    private sealed record AuthDto(string AccessToken, string RefreshToken, string Email);
}
