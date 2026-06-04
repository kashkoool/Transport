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
/// Promo codes: a manager creates one, a customer previews + redeems it (booking total is
/// discounted, redemption counted), and an invalid code is rejected.
/// </summary>
public sealed class PromoCodeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly int[] Seat5 = [5];
    private const string Password = "Str0ng!Passw0rd";

    [Fact]
    public async Task Customer_previews_and_redeems_a_percentage_promo_code()
    {
        var (manager, tripId) = await SeedCompanyTripAsync(price: 100_000m);

        var create = await PostJson(manager, "/api/vendor/promo-codes",
            new { code = "SAVE20", discountType = 0, discountValue = 20m });
        create.StatusCode.Should().Be(HttpStatusCode.OK);

        var (customer, _) = await factory.CreateCustomerClientAsync();

        // Preview: 20% off a 100,000 fare = 80,000.
        var preview = await customer.GetFromJsonAsync<PromoPreviewDto>(
            $"/api/bookings/promo-preview?tripId={tripId}&code=SAVE20&seats=1", Json);
        preview!.Discount.Should().Be(20_000m);
        preview.Total.Should().Be(80_000m);

        // Book with the code → discounted total.
        await customer.PostAsJsonAsync("/api/bookings/hold", new { tripId, seatNumbers = Seat5 });
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(new
            {
                tripId,
                promoCode = "SAVE20",
                passengers = new[] { new { firstName = "Promo", lastName = "User", seatNumber = 5 } },
            }),
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var booking = await (await customer.SendAsync(req)).Content.ReadFromJsonAsync<BookingDto>(Json);
        booking!.TotalAmount.Should().Be(80_000m);

        // Redemption was counted.
        var list = await manager.GetFromJsonAsync<PagedDto<PromoDto>>("/api/vendor/promo-codes", Json);
        list!.Data.Single(p => p.Code == "SAVE20").RedemptionCount.Should().Be(1);
    }

    [Fact]
    public async Task An_unknown_promo_code_is_rejected()
    {
        var (_, tripId) = await SeedCompanyTripAsync(price: 50_000m);
        var (customer, _) = await factory.CreateCustomerClientAsync();

        var preview = await customer.GetAsync($"/api/bookings/promo-preview?tripId={tripId}&code=NOPE&seats=1");
        preview.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Promo_preview_rejects_an_overlong_code()
    {
        var (_, tripId) = await SeedCompanyTripAsync(price: 50_000m);
        var (customer, _) = await factory.CreateCustomerClientAsync();

        // Bounded input: a code far past the max length is a clean 400, not unbounded processing.
        var longCode = new string('A', 100);
        var preview = await customer.GetAsync($"/api/bookings/promo-preview?tripId={tripId}&code={longCode}&seats=1");
        preview.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── helpers ───────────────────────────────────────────────────────────────────

    private async Task<(HttpClient Manager, Guid TripId)> SeedCompanyTripAsync(decimal price)
    {
        Guid companyId;
        var managerEmail = $"mgr-{Guid.NewGuid():N}@example.com";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var company = new Company("Promo Lines", $"v-{Guid.NewGuid():N}@example.com", null);
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
            new { busNumber = $"P-{Guid.NewGuid():N}".Substring(0, 8), seatCount = 40, type = 0, model = "Bus" }))
            .Content.ReadFromJsonAsync<IdDto>(Json);
        var trip = await (await PostJson(manager, "/api/vendor/trips", new
        {
            busId = bus!.Id,
            origin = "Damascus",
            destination = "Aleppo",
            departureUtc = DateTimeOffset.UtcNow.AddDays(3),
            arrivalUtc = DateTimeOffset.UtcNow.AddDays(3).AddHours(5),
            price,
            currency = "SYP",
        })).Content.ReadFromJsonAsync<IdDto>(Json);

        return (manager, trip!.Id);
    }

    private static async Task<HttpResponseMessage> PostJson(HttpClient client, string url, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        return await client.SendAsync(req);
    }

    private sealed record AuthDto(string AccessToken, string RefreshToken, string Email);
    private sealed record IdDto(Guid Id);
    private sealed record BookingDto(Guid BookingId, string Reference, decimal TotalAmount, string Currency);
    private sealed record PromoPreviewDto(string Code, decimal OriginalTotal, decimal Discount, decimal Total, string Currency);
    private sealed record PromoDto(Guid Id, string Code, string DiscountType, decimal DiscountValue, int? MaxRedemptions, int RedemptionCount, DateTimeOffset? ExpiresAtUtc, bool Active);
    private sealed record PagedDto<T>(IReadOnlyList<T> Data, int Total, int Page, int Limit, int TotalPages);
}
