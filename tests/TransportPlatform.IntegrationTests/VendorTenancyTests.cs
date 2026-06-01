using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// The headline guarantee of the vendor module: tenant isolation. A vendor manager only
/// ever sees and mutates their OWN company's fleet/trips — another tenant's resource id
/// resolves to 404 (never a 403 oracle), and listings never leak across companies.
/// </summary>
public sealed class VendorTenancyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Vendor_only_sees_and_mutates_their_own_company_resources()
    {
        // Two separate vendor companies, each with its own authenticated manager.
        var (companyA, tokenA) = await SeedCompanyWithManagerAsync();
        var (_, tokenB) = await SeedCompanyWithManagerAsync();

        var clientA = factory.CreateClient();
        var clientB = factory.CreateClient();

        // Vendor A adds a bus to their fleet.
        var addBus = await PostAsync(clientA, tokenA, "/api/vendor/buses",
            new { busNumber = "A-100", seatCount = 40, type = 0, model = "Volvo" });
        addBus.StatusCode.Should().Be(HttpStatusCode.OK);
        var busA = await addBus.Content.ReadFromJsonAsync<BusDto>(Json);

        // Vendor A schedules a trip on that bus.
        var addTrip = await PostAsync(clientA, tokenA, "/api/vendor/trips", new
        {
            busId = busA!.Id,
            origin = "Damascus",
            destination = "Aleppo",
            departureUtc = DateTimeOffset.UtcNow.AddDays(3),
            arrivalUtc = DateTimeOffset.UtcNow.AddDays(3).AddHours(5),
            price = 75_000m,
            currency = "SYP",
        });
        addTrip.StatusCode.Should().Be(HttpStatusCode.OK);
        var tripA = await addTrip.Content.ReadFromJsonAsync<TripDto>(Json);

        // Vendor B's fleet list is empty — A's bus does not leak across the tenant boundary.
        var busesB = await GetAsync(clientB, tokenB, "/api/vendor/buses");
        var pageB = await busesB.Content.ReadFromJsonAsync<PagedDto<BusDto>>(Json);
        pageB!.Total.Should().Be(0);

        // Vendor B cannot schedule a trip on Vendor A's bus → 404 (not found for this tenant).
        var crossTrip = await PostAsync(clientB, tokenB, "/api/vendor/trips", new
        {
            busId = busA.Id,
            origin = "Homs",
            destination = "Tartus",
            departureUtc = DateTimeOffset.UtcNow.AddDays(3),
            arrivalUtc = DateTimeOffset.UtcNow.AddDays(3).AddHours(2),
            price = 30_000m,
            currency = "SYP",
        });
        crossTrip.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Vendor B cannot cancel Vendor A's trip → 404.
        var crossCancel = await PostAsync(clientB, tokenB, $"/api/vendor/trips/{tripA!.Id}/cancel", new { });
        crossCancel.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Vendor A can cancel their own trip → 200.
        var ownCancel = await PostAsync(clientA, tokenA, $"/api/vendor/trips/{tripA.Id}/cancel", new { });
        ownCancel.StatusCode.Should().Be(HttpStatusCode.OK);

        _ = companyA; // (kept for readability of the seed tuple)
    }

    [Fact]
    public async Task Customer_token_cannot_reach_vendor_endpoints()
    {
        var client = factory.CreateClient();
        var email = $"c{Guid.NewGuid():N}@example.com";
        var reg = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "Str0ng!Passw0rd", fullName = "Cust Omer" });
        var auth = await reg.Content.ReadFromJsonAsync<AuthDto>(Json);

        var resp = await GetAsync(client, auth!.AccessToken, "/api/vendor/buses");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden); // authenticated but wrong role
    }

    // ── helpers ───────────────────────────────────────────────────────────────────

    /// <summary>Seed an active company + a VendorManager, and return an access token for them.</summary>
    private async Task<(Guid CompanyId, string AccessToken)> SeedCompanyWithManagerAsync()
    {
        Guid companyId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var company = new Company("Vendor Co", $"v-{Guid.NewGuid():N}@example.com", null);
            company.Activate();
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            companyId = company.Id;

            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            await identity.RegisterVendorManagerAsync(
                $"m-{Guid.NewGuid():N}@example.com", "Str0ng!Passw0rd", "Mgr", companyId);
        }

        // Log the manager in via the real endpoint so the token carries the company_id claim.
        // (Re-seed the email so we know the credentials.)
        var managerEmail = $"login-{Guid.NewGuid():N}@example.com";
        using (var scope = factory.Services.CreateScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            await identity.RegisterVendorManagerAsync(managerEmail, "Str0ng!Passw0rd", "Mgr", companyId);
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = managerEmail, password = "Str0ng!Passw0rd" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await login.Content.ReadFromJsonAsync<AuthDto>(Json);

        return (companyId, auth!.AccessToken);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string url, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(req);
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string token, string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(req);
    }

    private sealed record AuthDto(string AccessToken, string RefreshToken, string Email);
    private sealed record BusDto(Guid Id, string BusNumber, int SeatCount, string Type, string? Model);
    private sealed record TripDto(Guid Id, Guid BusId, string Origin, string Destination,
        DateTimeOffset DepartureUtc, DateTimeOffset ArrivalUtc, int SeatCount, decimal Price, string Currency, string Status);
    private sealed record PagedDto<T>(IReadOnlyList<T> Data, int Total, int Page, int Limit, int TotalPages);
}
