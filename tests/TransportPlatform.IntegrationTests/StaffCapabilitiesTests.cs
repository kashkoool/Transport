using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Identity;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// Staff capabilities + manager staff CRUD + search/trip filters: staff can run trip management
/// (but not delete), manager edit/delete of staff is tenant-scoped, and list/trip search filters
/// narrow results.
/// </summary>
public sealed class StaffCapabilitiesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Password = "Str0ng!Passw0rd";
    private static readonly DateTimeOffset Base = DateTimeOffset.UtcNow.AddDays(7);

    [Fact]
    public async Task Staff_can_manage_trips_but_not_delete_them()
    {
        var (manager, companyId) = await SeedManagerAsync();
        var staff = await CreateStaffClientAsync(companyId);
        var busId = await AddBusAsync(manager);

        // Staff schedules + starts a trip via the shared trip endpoints.
        var tripId = (await (await ScheduleTrip(staff, busId, Base, Base.AddHours(4)))
            .Content.ReadFromJsonAsync<TripDto>(Json))!.Id;
        (await PostEmpty(staff, $"/api/vendor/trips/{tripId}/start")).StatusCode.Should().Be(HttpStatusCode.OK);

        // But deleting a trip is manager-only → staff is forbidden.
        (await staff.DeleteAsync($"/api/vendor/trips/{tripId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        // …and the manager can delete a (booking-free) trip the staff scheduled.
        var spare = (await (await ScheduleTrip(staff, busId, Base.AddHours(10), Base.AddHours(14)))
            .Content.ReadFromJsonAsync<TripDto>(Json))!.Id;
        (await manager.DeleteAsync($"/api/vendor/trips/{spare}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Manager_edits_and_deletes_staff_only_within_their_company()
    {
        var (manager, _) = await SeedManagerAsync();
        var staffId = (await (await PostJson(manager, "/api/vendor/staff", new
        {
            email = $"s-{Guid.NewGuid():N}@example.com",
            password = Password,
            fullName = "Sam Staff",
            staffType = 2, // Employee
        })).Content.ReadFromJsonAsync<StaffDto>(Json))!.Id;

        // Edit within the company.
        var edit = await PutJson(manager, $"/api/vendor/staff/{staffId}", new { fullName = "Sam Renamed", staffType = 0 });
        edit.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A different company's manager can't touch this staff member (tenant isolation → 404).
        var (other, _) = await SeedManagerAsync();
        (await PutJson(other, $"/api/vendor/staff/{staffId}", new { fullName = "Hijack", staffType = 0 }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/api/vendor/staff/{staffId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The owning manager can delete.
        (await manager.DeleteAsync($"/api/vendor/staff/{staffId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Staff_search_narrows_the_roster()
    {
        var (manager, _) = await SeedManagerAsync();
        await PostJson(manager, "/api/vendor/staff", new { email = $"alice-{Guid.NewGuid():N}@example.com", password = Password, fullName = "Alice Anderson", staffType = 2 });
        await PostJson(manager, "/api/vendor/staff", new { email = $"bob-{Guid.NewGuid():N}@example.com", password = Password, fullName = "Bob Brown", staffType = 2 });

        var hits = await manager.GetFromJsonAsync<PagedDto<StaffDto>>("/api/vendor/staff?search=alice", Json);
        hits!.Data.Should().ContainSingle().Which.FullName.Should().Be("Alice Anderson");
    }

    [Fact]
    public async Task Trip_search_filters_by_max_price()
    {
        var (manager, _) = await SeedManagerAsync();
        var busId = await AddBusAsync(manager);
        var date = Base.Date;

        await ScheduleTrip(manager, busId, Base.AddHours(1), Base.AddHours(5), origin: "Aleppo", destination: "Hama", price: 40_000m);
        // A second bus so the two trips don't clash on the same vehicle.
        var bus2 = await AddBusAsync(manager);
        await ScheduleTrip(manager, bus2, Base.AddHours(2), Base.AddHours(6), origin: "Aleppo", destination: "Hama", price: 90_000m);

        var anon = factory.CreateClient();
        var cheap = await anon.GetFromJsonAsync<List<TripSummaryDto>>(
            $"/api/trips/search?origin=Aleppo&destination=Hama&date={date:yyyy-MM-dd}&maxPrice=50000", Json);
        cheap!.Should().OnlyContain(t => t.Price <= 50_000m);
        cheap.Should().Contain(t => t.Price == 40_000m);
        cheap.Should().NotContain(t => t.Price == 90_000m);
    }

    // ── helpers ───────────────────────────────────────────────────────────────────

    private async Task<(HttpClient Manager, Guid CompanyId)> SeedManagerAsync()
    {
        Guid companyId;
        var managerEmail = $"mgr-{Guid.NewGuid():N}@example.com";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var company = new Company("Staff Lines", $"v-{Guid.NewGuid():N}@example.com", null);
            company.Activate();
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            companyId = company.Id;
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            await identity.RegisterVendorManagerAsync(managerEmail, Password, "Mgr", companyId);
        }
        return (await LoginAsync(managerEmail), companyId);
    }

    private async Task<HttpClient> CreateStaffClientAsync(Guid companyId)
    {
        var email = $"staff-{Guid.NewGuid():N}@example.com";
        using (var scope = factory.Services.CreateScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            await identity.RegisterStaffAsync(companyId, email, Password, "Desk Staff", StaffType.Employee);
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
        var bus = await (await PostJson(manager, "/api/vendor/buses",
            new { busNumber = $"S-{Guid.NewGuid():N}"[..8], seatCount = 40, type = 0, model = "Bus", seatsPerRow = 4 }))
            .Content.ReadFromJsonAsync<BusDto>(Json);
        return bus!.Id;
    }

    private static Task<HttpResponseMessage> ScheduleTrip(
        HttpClient client, Guid busId, DateTimeOffset depart, DateTimeOffset arrive,
        string origin = "Damascus", string destination = "Aleppo", decimal price = 70_000m) =>
        PostJson(client, "/api/vendor/trips", new
        {
            busId, origin, destination, departureUtc = depart, arrivalUtc = arrive, price, currency = "SYP",
        });

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
    private sealed record BusDto(Guid Id, string BusNumber, int SeatCount, string Type, string? Model, Guid? DriverId, int SeatsPerRow);
    private sealed record TripDto(Guid Id, Guid BusId, string Origin, string Destination, DateTimeOffset DepartureUtc, DateTimeOffset ArrivalUtc, int SeatCount, decimal Price, string Currency, string Status);
    private sealed record StaffDto(Guid Id, string Email, string FullName, string StaffType, bool Suspended);
    private sealed record TripSummaryDto(Guid Id, string Origin, string Destination, DateTimeOffset DepartureUtc, DateTimeOffset ArrivalUtc, decimal Price, string Currency, int SeatCount, int AvailableSeats);
    private sealed record PagedDto<T>(IReadOnlyList<T> Data, int Total, int Page, int Limit, int TotalPages);
}
