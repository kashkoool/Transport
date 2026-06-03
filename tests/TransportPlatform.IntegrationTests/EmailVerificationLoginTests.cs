using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// The <c>Auth:RequireEmailVerification</c> toggle: when on, a password sign-in is refused until
/// the account's email is confirmed; when off (the default), an unverified user can still log in.
/// </summary>
public sealed class EmailVerificationLoginTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "Str0ng!Passw0rd";

    [Fact]
    public async Task With_the_toggle_on_an_unverified_user_is_blocked_until_they_verify()
    {
        // The flag is read from config at DI time, so boot a host with it enabled. It points at the
        // same Postgres container and shares the email capture, so register/verify still work.
        using var strict = factory.WithWebHostBuilder(b =>
            b.UseSetting("Auth:RequireEmailVerification", "true"));
        var client = strict.CreateClient();
        var email = $"ev{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = Password, fullName = "Verify First" });
        register.StatusCode.Should().Be(HttpStatusCode.OK);

        // Unverified → login refused (after a correct password, hence a specific 401).
        var blocked = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        blocked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Confirm the email via the emailed token, then the same login succeeds.
        var token = ExtractToken(factory.Emails.LastTo(email)!.HtmlBody);
        (await client.PostAsJsonAsync("/api/auth/verify-email", new { email, token }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var ok = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task With_the_toggle_off_an_unverified_user_can_log_in()
    {
        // Default factory → toggle off.
        var client = factory.CreateClient();
        var email = $"ev{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = Password, fullName = "No Verify" });

        var ok = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Pull the (URL-decoded) token out of a verify link in an email body.</summary>
    private static string ExtractToken(string htmlBody)
    {
        var body = WebUtility.HtmlDecode(htmlBody);
        const string marker = "token=";
        var start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = start;
        while (end < body.Length && body[end] is not ('"' or '&' or '<' or ' ' or '\n' or '\r'))
            end++;
        return Uri.UnescapeDataString(body[start..end]);
    }
}
