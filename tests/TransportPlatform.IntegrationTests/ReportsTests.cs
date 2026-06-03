using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Reports;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Infrastructure.Payments;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// Reporting + demand: a confirmed, paid booking shows up in the vendor summary, the per-trip CSV
/// export, and the admin system totals; the demand endpoint returns a well-formed forecast.
/// </summary>
public sealed class ReportsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Password = "Str0ng!Passw0rd";
    private static readonly int[] Seat5 = [5];

    [Fact]
    public async Task Vendor_and_admin_reports_reflect_a_confirmed_paid_booking()
    {
        var (managerClient, _, price) = await SetupConfirmedBookingAsync();

        // Vendor summary.
        var summary = await managerClient.GetFromJsonAsync<VendorSummaryDto>("/api/vendor/reports/summary", Json);
        summary!.ConfirmedBookings.Should().BeGreaterThanOrEqualTo(1);
        summary.SeatsSold.Should().BeGreaterThanOrEqualTo(1);
        summary.Revenue.Should().BeGreaterThanOrEqualTo(price);
        summary.OccupancyPct.Should().BeGreaterThan(0);

        // CSV export.
        var csv = await managerClient.GetAsync("/api/vendor/reports/trips/export");
        csv.StatusCode.Should().Be(HttpStatusCode.OK);
        csv.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var body = await csv.Content.ReadAsStringAsync();
        body.Should().Contain("Origin").And.Contain("Damascus");

        // Admin system summary (handler is auth-agnostic; assert it reflects the data).
        using var scope = factory.Services.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminSystemSummaryHandler>();
        var system = await admin.HandleAsync(default);
        system.Companies.Should().BeGreaterThan(0);
        system.ConfirmedBookings.Should().BeGreaterThanOrEqualTo(1);
        // Revenue is reported per currency now; the booking's currency line must cover its price.
        system.Revenue.Should().Contain(r => r.Amount >= price);
    }

    [Fact]
    public async Task Demand_prediction_endpoint_returns_a_forecast()
    {
        var (managerClient, _, _) = await SetupConfirmedBookingAsync();

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var prediction = await managerClient.GetFromJsonAsync<DemandDto>(
            $"/api/vendor/demand/predict?origin=Damascus&destination=Aleppo&date={date:yyyy-MM-dd}", Json);

        prediction.Should().NotBeNull();
        prediction!.PredictedBookings.Should().BeGreaterThanOrEqualTo(0);
        prediction.Confidence.Should().BeOneOf("low", "medium", "high");
    }

    // ── helpers ───────────────────────────────────────────────────────────────────

    private async Task<(HttpClient ManagerClient, Guid TripId, decimal Price)> SetupConfirmedBookingAsync()
    {
        var (companyId, managerClient) = await SeedCompanyWithManagerAsync();
        _ = companyId;

        var bus = await (await PostAsync(managerClient, "/api/vendor/buses",
            new { busNumber = $"R-{Guid.NewGuid():N}".Substring(0, 8), seatCount = 40, type = 0, model = "Bus" }))
            .Content.ReadFromJsonAsync<IdDto>(Json);

        const decimal price = 75_000m;
        var trip = await (await PostAsync(managerClient, "/api/vendor/trips", new
        {
            busId = bus!.Id,
            origin = "Damascus",
            destination = "Aleppo",
            departureUtc = DateTimeOffset.UtcNow.AddDays(3),
            arrivalUtc = DateTimeOffset.UtcNow.AddDays(3).AddHours(5),
            price,
            currency = "SYP",
        })).Content.ReadFromJsonAsync<IdDto>(Json);

        // A customer holds, books, pays → confirmed (so revenue/occupancy are real).
        var (customer, _) = await factory.CreateCustomerClientAsync();
        await customer.PostAsJsonAsync("/api/bookings/hold", new { tripId = trip!.Id, seatNumbers = Seat5 });
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(new
            {
                tripId = trip.Id,
                passengers = new[] { new { firstName = "Report", lastName = "Rider", seatNumber = 5 } },
            }),
        };
        create.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var booking = await (await customer.SendAsync(create)).Content.ReadFromJsonAsync<BookingDto>(Json);
        await customer.PostAsJsonAsync("/api/payments/checkout", new { bookingId = booking!.BookingId });
        await SendWebhookAsync(booking.Reference);

        return (managerClient, trip.Id, price);
    }

    private async Task<(Guid CompanyId, HttpClient ManagerClient)> SeedCompanyWithManagerAsync()
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (companyId, client);
    }

    private async Task SendWebhookAsync(string bookingReference)
    {
        var gateway = factory.Services.GetRequiredService<SandboxPaymentGateway>();
        var payload = JsonSerializer.Serialize(new
        {
            gatewayReference = $"SBX-{Guid.NewGuid():N}",
            bookingReference,
            succeeded = true,
        }, Json);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-Signature", gateway.ComputeSignature(payload));
        using var resp = await factory.CreateClient().SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        return await client.SendAsync(req);
    }

    private sealed record AuthDto(string AccessToken, string RefreshToken, string Email);
    private sealed record IdDto(Guid Id);
    private sealed record BookingDto(Guid BookingId, string Reference, decimal TotalAmount, string Currency);
    private sealed record VendorSummaryDto(int Trips, int ConfirmedBookings, int SeatsSold, int SeatsOffered, decimal Revenue, string Currency, double OccupancyPct);
    private sealed record DemandDto(string Origin, string Destination, int PredictedBookings, string Confidence, int SampleSize);
}
