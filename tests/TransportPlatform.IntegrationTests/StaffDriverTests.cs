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
/// Staff + driver management: lifecycle (create/list/suspend/reactivate, suspend blocks login),
/// driver assignment, and tenant isolation (a manager can only ever touch their own company).
/// </summary>
public sealed class StaffDriverTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Password = "Str0ng!Passw0rd";
    private const int Employee = 2; // StaffType.Employee (enums bind from their numeric value)

    [Fact]
    public async Task Manager_can_create_list_suspend_and_reactivate_staff()
    {
        var (_, token) = await SeedCompanyWithManagerAsync();
        var client = factory.CreateClient();
        var staffEmail = $"staff{Guid.NewGuid():N}@example.com";

        var create = await PostAsync(client, token, "/api/vendor/staff",
            new { email = staffEmail, password = Password, fullName = "Staff Member", staffType = Employee });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var staff = await create.Content.ReadFromJsonAsync<StaffDto>(Json);
        staff!.StaffType.Should().Be("Employee");

        // Appears in the company's staff list.
        var list = await GetAsync(client, token, "/api/vendor/staff");
        var page = await list.Content.ReadFromJsonAsync<PagedDto<StaffDto>>(Json);
        page!.Data.Should().ContainSingle(s => s.Id == staff.Id && !s.Suspended);

        // The new staff member can log in.
        var loginOk = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = staffEmail, password = Password });
        loginOk.StatusCode.Should().Be(HttpStatusCode.OK);

        // Suspend → login is blocked.
        (await PostAsync(client, token, $"/api/vendor/staff/{staff.Id}/suspend", new { }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var loginBlocked = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = staffEmail, password = Password });
        loginBlocked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Reactivate → login works again.
        (await PostAsync(client, token, $"/api/vendor/staff/{staff.Id}/reactivate", new { }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var loginAgain = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = staffEmail, password = Password });
        loginAgain.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Staff_management_is_tenant_isolated()
    {
        var (_, tokenA) = await SeedCompanyWithManagerAsync();
        var (_, tokenB) = await SeedCompanyWithManagerAsync();
        var client = factory.CreateClient();

        var create = await PostAsync(client, tokenA, "/api/vendor/staff",
            new { email = $"s{Guid.NewGuid():N}@example.com", password = Password, fullName = "A Staff", staffType = Employee });
        var staffA = await create.Content.ReadFromJsonAsync<StaffDto>(Json);

        // B's staff list does not contain A's staff.
        var listB = await GetAsync(client, tokenB, "/api/vendor/staff");
        var pageB = await listB.Content.ReadFromJsonAsync<PagedDto<StaffDto>>(Json);
        pageB!.Data.Should().NotContain(s => s.Id == staffA!.Id);

        // B cannot suspend A's staff → 404 (not a 403 oracle).
        var crossSuspend = await PostAsync(client, tokenB, $"/api/vendor/staff/{staffA!.Id}/suspend", new { });
        crossSuspend.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Driver_can_be_added_and_assigned_and_is_tenant_isolated()
    {
        var (_, tokenA) = await SeedCompanyWithManagerAsync();
        var (_, tokenB) = await SeedCompanyWithManagerAsync();
        var client = factory.CreateClient();

        var busA = await (await PostAsync(client, tokenA, "/api/vendor/buses",
            new { busNumber = $"D-{Guid.NewGuid():N}".Substring(0, 10), seatCount = 30, type = 0, model = "Bus" }))
            .Content.ReadFromJsonAsync<BusDto>(Json);
        var driverA = await (await PostAsync(client, tokenA, "/api/vendor/drivers",
            new { fullName = "Driver A", phone = "+963999000111", licenseNumber = "L-1" }))
            .Content.ReadFromJsonAsync<DriverDto>(Json);

        // Assign own driver to own bus.
        var assign = await PostAsync(client, tokenA, $"/api/vendor/buses/{busA!.Id}/driver", new { driverId = driverA!.Id });
        assign.StatusCode.Should().Be(HttpStatusCode.OK);
        var assigned = await assign.Content.ReadFromJsonAsync<BusDto>(Json);
        assigned!.DriverId.Should().Be(driverA.Id);

        // B cannot assign A's driver to A's bus (both resolve as not-found for B) → 404.
        var crossAssign = await PostAsync(client, tokenB, $"/api/vendor/buses/{busA.Id}/driver", new { driverId = driverA.Id });
        crossAssign.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // B's own bus cannot be assigned A's driver → 404 on the driver lookup.
        var busB = await (await PostAsync(client, tokenB, "/api/vendor/buses",
            new { busNumber = $"E-{Guid.NewGuid():N}".Substring(0, 10), seatCount = 30, type = 0, model = "Bus" }))
            .Content.ReadFromJsonAsync<BusDto>(Json);
        var crossDriver = await PostAsync(client, tokenB, $"/api/vendor/buses/{busB!.Id}/driver", new { driverId = driverA.Id });
        crossDriver.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── helpers ───────────────────────────────────────────────────────────────────

    private async Task<(Guid CompanyId, string AccessToken)> SeedCompanyWithManagerAsync()
    {
        Guid companyId;
        var managerEmail = $"mgr-{Guid.NewGuid():N}@example.com";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var company = new Company("Vendor Co", $"v-{Guid.NewGuid():N}@example.com", null);
            company.Activate();
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            companyId = company.Id;

            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            await identity.RegisterVendorManagerAsync(managerEmail, Password, "Mgr", companyId);
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = managerEmail, password = Password });
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
    private sealed record StaffDto(Guid Id, string Email, string FullName, string StaffType, bool Suspended);
    private sealed record DriverDto(Guid Id, string FullName, string? Phone, string? LicenseNumber);
    private sealed record BusDto(Guid Id, string BusNumber, int SeatCount, string Type, string? Model, Guid? DriverId);
    private sealed record PagedDto<T>(IReadOnlyList<T> Data, int Total, int Page, int Limit, int TotalPages);
}
