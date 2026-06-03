using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Trips;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// Seat maps, trip waypoints and passenger documents: a manager sets the bus layout + stops,
/// the public seat-map/stops reads reflect them, and a booking with an ID document persists it
/// and marks the seat taken on the next seat-map read.
/// </summary>
public sealed class SeatMapStopsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly int[] Seat7 = [7];
    private const string Password = "Str0ng!Passw0rd";

    [Fact]
    public async Task Seat_map_exposes_layout_and_stops_are_returned_in_order()
    {
        var manager = await SeedManagerAsync();

        // A bus with a custom 2+3 layout (5 per row).
        var bus = await (await PostJson(manager, "/api/vendor/buses",
            new { busNumber = $"S-{Guid.NewGuid():N}"[..8], seatCount = 40, type = 0, model = "Coach", seatsPerRow = 5 }))
            .Content.ReadFromJsonAsync<BusDto>(Json);
        bus!.SeatsPerRow.Should().Be(5);

        var tripId = (await (await PostJson(manager, "/api/vendor/trips", new
        {
            busId = bus.Id,
            origin = "Damascus",
            destination = "Aleppo",
            departureUtc = DateTimeOffset.UtcNow.AddDays(3),
            arrivalUtc = DateTimeOffset.UtcNow.AddDays(3).AddHours(5),
            price = 70_000m,
            currency = "SYP",
        })).Content.ReadFromJsonAsync<TripDto>(Json))!.Id;

        // Public seat-map: layout flows through, nothing taken yet.
        var client = factory.CreateClient();
        var map = await client.GetFromJsonAsync<SeatMapDto>($"/api/trips/{tripId}/seat-map", Json);
        map!.SeatCount.Should().Be(40);
        map.SeatsPerRow.Should().Be(5);
        map.TakenSeats.Should().BeEmpty();

        // Set two stops (deliberately out of departure order in the request).
        var set = await PutJson(manager, $"/api/vendor/trips/{tripId}/stops", new
        {
            stops = new[]
            {
                new TripStopInput("Homs", DateTimeOffset.UtcNow.AddDays(3).AddHours(2), null),
                new TripStopInput("Hama", null, null),
            },
        });
        set.StatusCode.Should().Be(HttpStatusCode.OK);

        // Public stops: returned in sequence 1..N as supplied.
        var stops = await client.GetFromJsonAsync<List<TripStopDto>>($"/api/trips/{tripId}/stops", Json);
        stops!.Should().HaveCount(2);
        stops[0].Sequence.Should().Be(1);
        stops[0].Name.Should().Be("Homs");
        stops[1].Sequence.Should().Be(2);
        stops[1].Name.Should().Be("Hama");

        // Replacing stops fully overwrites the previous set (no accumulation).
        var replace = await PutJson(manager, $"/api/vendor/trips/{tripId}/stops",
            new { stops = new[] { new TripStopInput("Hama", null, null) } });
        replace.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterReplace = await client.GetFromJsonAsync<List<TripStopDto>>($"/api/trips/{tripId}/stops", Json);
        afterReplace!.Should().ContainSingle().Which.Name.Should().Be("Hama");
    }

    [Fact]
    public async Task Booking_with_a_passenger_document_persists_it_and_marks_the_seat_taken()
    {
        var (tripId, _, _) = await factory.SeedTripAsync();
        var (customer, _) = await factory.CreateCustomerClientAsync();

        await customer.PostAsJsonAsync("/api/bookings/hold", new { tripId, seatNumbers = Seat7 });

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(new
            {
                tripId,
                passengers = new[]
                {
                    new { firstName = "Doc", lastName = "Holder", seatNumber = 7, documentType = "Passport", documentNumber = "X1234567" },
                },
            }),
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var booking = await customer.SendAsync(req);
        booking.StatusCode.Should().Be(HttpStatusCode.OK);

        // The document survived the round-trip.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var passenger = await db.Set<Domain.Bookings.Passenger>().AsNoTracking()
                .SingleAsync(p => p.SeatNumber == 7 && p.DocumentNumber == "X1234567");
            passenger.DocumentType.Should().Be("Passport");
        }

        // The held seat now shows as taken on the public seat-map.
        var client = factory.CreateClient();
        var map = await client.GetFromJsonAsync<SeatMapDto>($"/api/trips/{tripId}/seat-map", Json);
        map!.TakenSeats.Should().Contain(7);
    }

    // ── helpers ───────────────────────────────────────────────────────────────────

    private async Task<HttpClient> SeedManagerAsync()
    {
        Guid companyId;
        var managerEmail = $"mgr-{Guid.NewGuid():N}@example.com";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var company = new Company("Map Lines", $"v-{Guid.NewGuid():N}@example.com", null);
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
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

    private sealed record AuthDto(string AccessToken, string RefreshToken, string Email);
    private sealed record BusDto(Guid Id, string BusNumber, int SeatCount, string Type, string? Model, Guid? DriverId, int SeatsPerRow);
    private sealed record TripDto(Guid Id, Guid BusId, string Origin, string Destination, DateTimeOffset DepartureUtc, DateTimeOffset ArrivalUtc, int SeatCount, decimal Price, string Currency, string Status);
    private sealed record SeatMapDto(Guid TripId, int SeatCount, int SeatsPerRow, IReadOnlyList<int> TakenSeats);
    private sealed record TripStopDto(int Sequence, string Name, DateTimeOffset? ArrivalUtc, DateTimeOffset? DepartureUtc);
}
