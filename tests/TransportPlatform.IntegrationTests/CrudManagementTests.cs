using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Identity;
using TransportPlatform.Infrastructure.Identity;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// Vendor/admin CRUD: bus edit + delete (blocked while in use), trip edit + status lifecycle +
/// delete, company profile view/edit, and admin company update + delete (blocked while it has data).
/// </summary>
public sealed class CrudManagementTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Password = "Str0ng!Passw0rd";

    [Fact]
    public async Task Manager_edits_buses_runs_trip_lifecycle_and_deletes_unused_resources()
    {
        var (_, manager) = await SeedCompanyWithManagerAsync();
        var busId = await AddBusAsync(manager);

        // Edit the bus.
        var edit = await PutJson(manager, $"/api/vendor/buses/{busId}",
            new { seatCount = 52, type = 1, model = "Volvo 9700" });
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await edit.Content.ReadFromJsonAsync<BusDto>(Json))!.SeatCount.Should().Be(52);

        // Schedule a trip on it, then bus delete is blocked.
        var tripId = await ScheduleTripAsync(manager, busId);
        (await manager.DeleteAsync($"/api/vendor/buses/{busId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Edit the (booking-free) trip, then run its lifecycle.
        var tripEdit = await PutJson(manager, $"/api/vendor/trips/{tripId}", new
        {
            origin = "Damascus",
            destination = "Tartus",
            departureUtc = DateTimeOffset.UtcNow.AddDays(4),
            arrivalUtc = DateTimeOffset.UtcNow.AddDays(4).AddHours(4),
            price = 80_000m,
            currency = "SYP",
        });
        tripEdit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await tripEdit.Content.ReadFromJsonAsync<TripDto>(Json))!.Destination.Should().Be("Tartus");

        (await PostEmpty(manager, $"/api/vendor/trips/{tripId}/start")).StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await PostEmpty(manager, $"/api/vendor/trips/{tripId}/complete");
        completed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await completed.Content.ReadFromJsonAsync<TripDto>(Json))!.Status.Should().Be("Completed");

        // A spare, unused bus can be deleted.
        var spare = await AddBusAsync(manager);
        (await manager.DeleteAsync($"/api/vendor/buses/{spare}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Company_profile_can_be_viewed_and_edited_by_vendor_and_admin()
    {
        var (companyId, manager) = await SeedCompanyWithManagerAsync();

        var profile = await manager.GetFromJsonAsync<CompanyDto>("/api/vendor/company", Json);
        profile!.Id.Should().Be(companyId);

        var vendorEdit = await PutJson(manager, "/api/vendor/company", new { name = "Renamed Lines", phone = "+963900111222" });
        vendorEdit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await vendorEdit.Content.ReadFromJsonAsync<CompanyDto>(Json))!.Name.Should().Be("Renamed Lines");

        var admin = await CreateAdminClientAsync();
        var adminEdit = await PutJson(admin, $"/api/admin/companies/{companyId}", new { name = "Admin Renamed", phone = (string?)null });
        adminEdit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await adminEdit.Content.ReadFromJsonAsync<CompanyDto>(Json))!.Name.Should().Be("Admin Renamed");
    }

    [Fact]
    public async Task Admin_deletes_an_empty_company_but_not_one_with_data()
    {
        var admin = await CreateAdminClientAsync();

        // Empty company → deletable.
        var (emptyId, _) = await SeedCompanyWithManagerAsync();
        (await admin.DeleteAsync($"/api/admin/companies/{emptyId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Company with a bus → blocked.
        var (withDataId, manager) = await SeedCompanyWithManagerAsync();
        await AddBusAsync(manager);
        (await admin.DeleteAsync($"/api/admin/companies/{withDataId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── helpers ───────────────────────────────────────────────────────────────────

    private async Task<(Guid CompanyId, HttpClient ManagerClient)> SeedCompanyWithManagerAsync()
    {
        Guid companyId;
        var managerEmail = $"mgr-{Guid.NewGuid():N}@example.com";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var company = new Company("Test Lines", $"v-{Guid.NewGuid():N}@example.com", null);
            company.Activate();
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            companyId = company.Id;
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            await identity.RegisterVendorManagerAsync(managerEmail, Password, "Mgr", companyId);
        }
        return (companyId, await LoginAsync(managerEmail));
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var email = $"admin-{Guid.NewGuid():N}@example.com";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roles.RoleExistsAsync(UserRoles.Admin))
                await roles.CreateAsync(new IdentityRole<Guid>(UserRoles.Admin));
            var admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            (await users.CreateAsync(admin, Password)).Succeeded.Should().BeTrue();
            await users.AddToRoleAsync(admin, UserRoles.Admin);
        }
        return await LoginAsync(email);
    }

    private async Task<HttpClient> LoginAsync(string email)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await login.Content.ReadFromJsonAsync<AuthDto>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<Guid> AddBusAsync(HttpClient manager)
    {
        var resp = await PostJson(manager, "/api/vendor/buses",
            new { busNumber = $"M-{Guid.NewGuid():N}".Substring(0, 8), seatCount = 40, type = 0, model = "Bus" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await resp.Content.ReadFromJsonAsync<BusDto>(Json))!.Id;
    }

    private static async Task<Guid> ScheduleTripAsync(HttpClient manager, Guid busId)
    {
        var resp = await PostJson(manager, "/api/vendor/trips", new
        {
            busId,
            origin = "Damascus",
            destination = "Aleppo",
            departureUtc = DateTimeOffset.UtcNow.AddDays(4),
            arrivalUtc = DateTimeOffset.UtcNow.AddDays(4).AddHours(5),
            price = 70_000m,
            currency = "SYP",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await resp.Content.ReadFromJsonAsync<TripDto>(Json))!.Id;
    }

    private static async Task<HttpResponseMessage> PostJson(HttpClient client, string url, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> PutJson(HttpClient client, string url, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) };
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> PostEmpty(HttpClient client, string url)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        return await client.SendAsync(req);
    }

    private sealed record AuthDto(string AccessToken, string RefreshToken, string Email);
    private sealed record BusDto(Guid Id, string BusNumber, int SeatCount, string Type, string? Model, Guid? DriverId);
    private sealed record TripDto(Guid Id, Guid BusId, string Origin, string Destination, DateTimeOffset DepartureUtc, DateTimeOffset ArrivalUtc, int SeatCount, decimal Price, string Currency, string Status);
    private sealed record CompanyDto(Guid Id, string Name, string Email, string? Phone, string Status);
}
