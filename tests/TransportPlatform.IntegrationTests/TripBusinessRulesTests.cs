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
/// Bus/trip business rules: a bus can't run overlapping scheduled trips, editing a bus's seat
/// count cascades to its scheduled trips (and is guarded against shrinking below sold seats), and
/// a cancelled trip can be re-activated unless the bus is busy again.
/// </summary>
public sealed class TripBusinessRulesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Password = "Str0ng!Passw0rd";
    private static readonly DateTimeOffset Base = DateTimeOffset.UtcNow.AddDays(5);

    [Fact]
    public async Task A_bus_cannot_run_two_overlapping_scheduled_trips()
    {
        var (manager, busId) = await SeedManagerWithBusAsync();

        (await ScheduleTrip(manager, busId, Base, Base.AddHours(5))).StatusCode.Should().Be(HttpStatusCode.OK);

        // Overlaps the first window → rejected.
        var overlap = await ScheduleTrip(manager, busId, Base.AddHours(1), Base.AddHours(6));
        overlap.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // A later, non-overlapping window is fine.
        (await ScheduleTrip(manager, busId, Base.AddHours(10), Base.AddHours(15)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Editing_a_bus_seat_count_cascades_and_is_guarded_against_sold_seats()
    {
        var (manager, busId) = await SeedManagerWithBusAsync(seatCount: 40);
        var tripId = (await (await ScheduleTrip(manager, busId, Base, Base.AddHours(5)))
            .Content.ReadFromJsonAsync<TripDto>(Json))!.Id;

        // Sell seat 40 at the desk (immediately confirmed → a seat assignment).
        var sell = await PostJson(manager, "/api/vendor/bookings", new
        {
            tripId,
            customerEmail = $"rider-{Guid.NewGuid():N}@example.com",
            passengers = new[] { new { firstName = "Walk", lastName = "In", seatNumber = 40 } },
        });
        sell.StatusCode.Should().Be(HttpStatusCode.OK);

        // Shrinking below the sold seat is blocked.
        var shrink = await PutJson(manager, $"/api/vendor/buses/{busId}",
            new { seatCount = 30, type = 0, model = "Bus", seatsPerRow = 4 });
        shrink.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Growing succeeds and cascades to the trip (seat-map reflects the new capacity).
        var grow = await PutJson(manager, $"/api/vendor/buses/{busId}",
            new { seatCount = 50, type = 0, model = "Bus", seatsPerRow = 4 });
        grow.StatusCode.Should().Be(HttpStatusCode.OK);

        var map = await factory.CreateClient().GetFromJsonAsync<SeatMapDto>($"/api/trips/{tripId}/seat-map", Json);
        map!.SeatCount.Should().Be(50);
    }

    [Fact]
    public async Task A_cancelled_trip_reverts_unless_the_bus_is_busy()
    {
        var (manager, busId) = await SeedManagerWithBusAsync();
        var tripId = (await (await ScheduleTrip(manager, busId, Base, Base.AddHours(5)))
            .Content.ReadFromJsonAsync<TripDto>(Json))!.Id;

        (await PostEmpty(manager, $"/api/vendor/trips/{tripId}/cancel")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Bus is free → revert succeeds.
        var revert = await PostEmpty(manager, $"/api/vendor/trips/{tripId}/revert");
        revert.StatusCode.Should().Be(HttpStatusCode.OK);
        (await revert.Content.ReadFromJsonAsync<TripDto>(Json))!.Status.Should().Be("Scheduled");

        // Cancel again, then occupy the window with another trip → revert is now blocked.
        (await PostEmpty(manager, $"/api/vendor/trips/{tripId}/cancel")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ScheduleTrip(manager, busId, Base, Base.AddHours(5))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PostEmpty(manager, $"/api/vendor/trips/{tripId}/revert")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── helpers ───────────────────────────────────────────────────────────────────

    private async Task<(HttpClient Manager, Guid BusId)> SeedManagerWithBusAsync(int seatCount = 40)
    {
        Guid companyId;
        var managerEmail = $"mgr-{Guid.NewGuid():N}@example.com";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var company = new Company("Rules Lines", $"v-{Guid.NewGuid():N}@example.com", null);
            company.Activate();
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            companyId = company.Id;
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            await identity.RegisterVendorManagerAsync(managerEmail, Password, "Mgr", companyId);
        }

        var manager = factory.CreateClient();
        var login = await manager.PostAsJsonAsync("/api/auth/login", new { email = managerEmail, password = Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await login.Content.ReadFromJsonAsync<AuthDto>(Json);
        manager.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var bus = await (await PostJson(manager, "/api/vendor/buses",
            new { busNumber = $"R-{Guid.NewGuid():N}"[..8], seatCount, type = 0, model = "Bus", seatsPerRow = 4 }))
            .Content.ReadFromJsonAsync<BusDto>(Json);
        return (manager, bus!.Id);
    }

    private static Task<HttpResponseMessage> ScheduleTrip(HttpClient manager, Guid busId, DateTimeOffset depart, DateTimeOffset arrive) =>
        PostJson(manager, "/api/vendor/trips", new
        {
            busId,
            origin = "Damascus",
            destination = "Aleppo",
            departureUtc = depart,
            arrivalUtc = arrive,
            price = 70_000m,
            currency = "SYP",
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
    private sealed record SeatMapDto(Guid TripId, int SeatCount, int SeatsPerRow, IReadOnlyList<int> TakenSeats);
}
