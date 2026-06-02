using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Domain.Bookings.Events;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// Exercises the email-driven auth flows against the real API + Postgres: email verification,
/// forgot/reset password, and the booking-confirmation email dispatch. Emails are captured by a
/// test sender (see <see cref="ApiFactory.Emails"/>) so tokens can be extracted from the links.
/// </summary>
public sealed class AuthEmailFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "Str0ng!Passw0rd";

    [Fact]
    public async Task Register_sends_a_verification_email_that_confirms_the_account()
    {
        var client = factory.CreateClient();
        var email = $"v{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = Password, fullName = "Verify Me" });
        register.StatusCode.Should().Be(HttpStatusCode.OK);

        var sent = factory.Emails.LastTo(email);
        sent.Should().NotBeNull("registration should trigger a verification email");
        var token = ExtractToken(sent!.HtmlBody);
        token.Should().NotBeNullOrEmpty();

        var verify = await client.PostAsJsonAsync("/api/auth/verify-email", new { email, token });
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Forgot_password_emails_a_reset_link_that_changes_the_password()
    {
        var client = factory.CreateClient();
        var email = $"r{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = Password, fullName = "Reset Me" });

        var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        forgot.StatusCode.Should().Be(HttpStatusCode.OK);

        var resetEmail = factory.Emails.Sent.LastOrDefault(m => m.ToEmail == email && m.Subject.Contains("Reset"));
        resetEmail.Should().NotBeNull("forgot-password should send a reset email");
        var token = ExtractToken(resetEmail!.HtmlBody);

        const string newPassword = "N3w!Str0ngPass";
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new { email, token, newPassword });
        reset.StatusCode.Should().Be(HttpStatusCode.OK);

        // The new password works; the old one no longer does.
        var withNew = await client.PostAsJsonAsync("/api/auth/login", new { email, password = newPassword });
        withNew.StatusCode.Should().Be(HttpStatusCode.OK);
        var withOld = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        withOld.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Forgot_password_for_an_unknown_email_still_returns_ok_and_sends_nothing()
    {
        var client = factory.CreateClient();
        var unknown = $"nobody{Guid.NewGuid():N}@example.com";

        var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = unknown });

        forgot.StatusCode.Should().Be(HttpStatusCode.OK); // anti-enumeration: same response
        factory.Emails.LastTo(unknown).Should().BeNull();
    }

    [Fact]
    public async Task Booking_confirmed_event_dispatches_a_confirmation_email()
    {
        using var scope = factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IIntegrationEventDispatcher>();
        var rider = $"rider{Guid.NewGuid():N}@example.com";
        var evt = new BookingConfirmedDomainEvent(Guid.NewGuid(), "BK-EMAILTEST", rider);

        await dispatcher.DispatchAsync(typeof(BookingConfirmedDomainEvent).FullName!, JsonSerializer.Serialize(evt));

        var email = factory.Emails.LastTo(rider);
        email.Should().NotBeNull();
        email!.Subject.Should().Contain("BK-EMAILTEST");
    }

    /// <summary>Pull the (URL-decoded) token out of a verify/reset link in an email body.</summary>
    private static string ExtractToken(string htmlBody)
    {
        var body = WebUtility.HtmlDecode(htmlBody); // links are HTML-encoded (&amp;) in the body
        const string marker = "token=";
        var start = body.IndexOf(marker, StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, "the email link must carry a token");
        start += marker.Length;
        var end = start;
        while (end < body.Length && body[end] is not ('"' or '&' or '<' or ' ' or '\n' or '\r'))
            end++;
        return Uri.UnescapeDataString(body[start..end]);
    }
}
