using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Domain.Notifications;
using TransportPlatform.Infrastructure.Identity;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// Authenticated self-service: change-password (verifies + rotates), profile view/edit, and
/// owner-scoped notification delete.
/// </summary>
public sealed class AuthSelfServiceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Password = "Str0ng!Passw0rd";

    [Fact]
    public async Task Change_password_requires_the_correct_current_password_and_rotates_it()
    {
        var (client, email) = await factory.CreateCustomerClientAsync();

        // Wrong current password → generic 401, no rotation.
        var wrong = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "Wr0ng!Passw0rd", newPassword = "N3w!Str0ngPass" });
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Correct current password → changed.
        const string newPassword = "N3w!Str0ngPass";
        var ok = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = Password, newPassword });
        ok.StatusCode.Should().Be(HttpStatusCode.OK);

        // New password logs in; the old one no longer does.
        var anon = factory.CreateClient();
        (await anon.PostAsJsonAsync("/api/auth/login", new { email, password = newPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await anon.PostAsJsonAsync("/api/auth/login", new { email, password = Password }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Profile_can_be_viewed_and_updated()
    {
        var (client, email) = await factory.CreateCustomerClientAsync();

        var profile = await client.GetFromJsonAsync<ProfileDto>("/api/auth/profile", Json);
        profile!.Email.Should().Be(email);
        profile.FullName.Should().Be("Test Customer");

        var update = await client.PutAsJsonAsync("/api/auth/profile",
            new { fullName = "Renamed Customer", phone = "+963900111222" });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<ProfileDto>(Json);
        updated!.FullName.Should().Be("Renamed Customer");
        updated.Phone.Should().Be("+963900111222");

        // Persisted.
        (await client.GetFromJsonAsync<ProfileDto>("/api/auth/profile", Json))!.FullName
            .Should().Be("Renamed Customer");
    }

    [Fact]
    public async Task A_customer_can_delete_only_their_own_notification()
    {
        var (alice, aliceEmail) = await factory.CreateCustomerClientAsync();
        var (bob, _) = await factory.CreateCustomerClientAsync();

        Guid notificationId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var aliceId = (await db.Users.AsNoTracking().FirstAsync(u => u.Email == aliceEmail)).Id;
            var notification = new Notification(aliceId, "Welcome", "Thanks for joining.", "info");
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();
            notificationId = notification.Id;
        }

        // Bob can't delete Alice's notification (scoped → 404, not 403, to avoid leaking existence).
        (await bob.DeleteAsync($"/api/notifications/{notificationId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Alice can, and it's gone.
        (await alice.DeleteAsync($"/api/notifications/{notificationId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await alice.GetFromJsonAsync<PagedDto<ProfileDto>>("/api/notifications", Json);
        list!.Total.Should().Be(0);
    }

    [Fact]
    public async Task Login_does_not_reveal_lockout_to_a_wrong_password()
    {
        var (_, email) = await factory.CreateCustomerClientAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            await users.SetLockoutEnabledAsync(user!, true);
            await users.SetLockoutEndDateAsync(user!, DateTimeOffset.MaxValue);
        }

        var anon = factory.CreateClient();

        // Wrong password on a locked account → generic error (no "this account exists + is locked" leak).
        var wrong = await anon.PostAsJsonAsync("/api/auth/login", new { email, password = "Wr0ng!Passw0rd" });
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CodeOf(wrong)).Should().Be("auth.invalid_credentials");

        // Correct password may reveal lockout (only after credentials are proven).
        var correct = await anon.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        correct.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CodeOf(correct)).Should().Be("auth.locked_out");
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("code", out var c) ? c.GetString() : null;
    }

    private sealed record ProfileDto(string Email, string FullName, string? Phone, string[] Roles);
    private sealed record PagedDto<T>(IReadOnlyList<T> Data, int Total, int Page, int Limit, int TotalPages);
}
