using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Infrastructure.Identity;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// Exercises external-login (Google) account resolution against the real Identity stores + Postgres.
/// The live Google round-trip isn't automated; this locks in the security-critical link/create
/// decision in <c>IdentityService.FindOrCreateExternalUserAsync</c>.
/// </summary>
public sealed class ExternalLoginTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Provider = "Google";

    private async Task<AuthenticatedUser> FindOrCreateAsync(string key, string email, bool verified, string? name = "Ext User")
    {
        using var scope = factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        return await identity.FindOrCreateExternalUserAsync(Provider, key, email, name, verified);
    }

    [Fact]
    public async Task First_google_sign_in_creates_a_confirmed_customer_and_is_idempotent()
    {
        var email = $"g{Guid.NewGuid():N}@example.com";
        var key = $"google-{Guid.NewGuid():N}";

        var first = await FindOrCreateAsync(key, email, verified: true);
        first.Roles.Should().Contain("Customer");

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var created = await users.FindByEmailAsync(email);
            created.Should().NotBeNull();
            created!.EmailConfirmed.Should().BeTrue("Google asserted the email is verified");
        }

        // Same provider key on a later sign-in returns the same user — no duplicate account.
        var second = await FindOrCreateAsync(key, email, verified: true);
        second.UserId.Should().Be(first.UserId);
    }

    [Fact]
    public async Task Google_links_to_an_existing_verified_account_instead_of_duplicating()
    {
        var email = $"link{Guid.NewGuid():N}@example.com";
        Guid existingId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var u = new ApplicationUser { UserName = email, Email = email, FullName = "Existing", EmailConfirmed = true };
            (await users.CreateAsync(u, "Str0ng!Passw0rd")).Succeeded.Should().BeTrue();
            existingId = u.Id;
        }

        var linked = await FindOrCreateAsync($"google-{Guid.NewGuid():N}", email, verified: true);

        linked.UserId.Should().Be(existingId); // linked to the existing account, not a new one
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var u = await users.FindByEmailAsync(email);
            var logins = await users.GetLoginsAsync(u!);
            logins.Should().Contain(l => l.LoginProvider == Provider);
        }
    }

    [Fact]
    public async Task Google_refuses_to_link_to_an_unverified_local_account()
    {
        var email = $"unverified{Guid.NewGuid():N}@example.com";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var u = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = false };
            (await users.CreateAsync(u, "Str0ng!Passw0rd")).Succeeded.Should().BeTrue();
        }

        // Provider not asserting verification + unconfirmed local account → refuse (anti-takeover).
        var act = async () => await FindOrCreateAsync($"google-{Guid.NewGuid():N}", email, verified: false);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
