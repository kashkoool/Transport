using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Identity;
using TransportPlatform.Domain.Payments;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// Staff/manager desk: counter (cash) booking confirms immediately, overbooking is blocked, and
/// a staff member (VendorOrStaff) can sell + cancel/refund — cash refunds are flagged manual.
/// </summary>
public sealed class CounterBookingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Password = "Str0ng!Passw0rd";

    [Fact]
    public async Task Manager_sells_a_counter_ticket_that_is_confirmed_paid_cash_and_listed()
    {
        var (managerClient, tripId) = await SeedCompanyTripAsync();

        var booking = await Sell(managerClient, tripId, seat: 3, email: "walkin1@example.com");
        booking.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await booking.Content.ReadFromJsonAsync<BookingDto>(Json);
        dto!.Reference.Should().StartWith("BK-");

        var list = await managerClient.GetFromJsonAsync<PagedDto<CompanyBookingDto>>("/api/vendor/bookings", Json);
        list!.Data.Should().Contain(b => b.BookingId == dto.BookingId && b.Status == "Confirmed");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = await db.Payments.SingleAsync(p => p.BookingId == dto.BookingId);
        payment.Gateway.Should().Be("Cash");
        payment.Status.Should().Be(PaymentStatus.Completed);
        (await db.SeatAssignments.CountAsync(a => a.BookingId == dto.BookingId)).Should().Be(1);
    }

    [Fact]
    public async Task Counter_booking_cannot_oversell_a_taken_seat()
    {
        var (managerClient, tripId) = await SeedCompanyTripAsync();

        (await Sell(managerClient, tripId, seat: 7, email: "a@example.com")).StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await Sell(managerClient, tripId, seat: 7, email: "b@example.com");
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Staff_can_sell_at_the_desk_and_cancel_with_a_manual_cash_refund()
    {
        var (managerClient, tripId, companyId) = await SeedCompanyTripWithCompanyAsync();

        // Manager provisions a staff member; staff logs in (VendorOrStaff lets them use the desk).
        var staffEmail = $"staff{Guid.NewGuid():N}@example.com";
        var createStaff = await PostJson(managerClient, "/api/vendor/staff",
            new { email = staffEmail, password = Password, fullName = "Desk Staff", staffType = (int)StaffType.Employee });
        createStaff.StatusCode.Should().Be(HttpStatusCode.OK);
        var staffClient = await LoginAsync(staffEmail);

        var sold = await Sell(staffClient, tripId, seat: 9, email: "walkin2@example.com");
        sold.StatusCode.Should().Be(HttpStatusCode.OK);
        var booking = await sold.Content.ReadFromJsonAsync<BookingDto>(Json);

        var cancel = await PostJson(staffClient, $"/api/vendor/bookings/{booking!.BookingId}/cancel", new { });
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await cancel.Content.ReadFromJsonAsync<CancelResult>(Json);
        result!.Status.Should().Be("cancelled");
        result.RefundInitiated.Should().BeFalse("cash refunds are handled manually at the desk");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.SeatAssignments.CountAsync(a => a.BookingId == booking.BookingId)).Should().Be(0); // seat freed
        var refund = await db.Refunds.SingleAsync(r => r.BookingId == booking.BookingId);
        refund.Status.Should().Be(RefundStatus.Pending); // manual cash refund recorded
        _ = companyId;
    }

    // ── helpers ───────────────────────────────────────────────────────────────────

    private static Task<HttpResponseMessage> Sell(HttpClient client, Guid tripId, int seat, string email) =>
        PostJson(client, "/api/vendor/bookings", new
        {
            tripId,
            customerEmail = email,
            passengers = new[] { new { firstName = "Walk", lastName = "In", seatNumber = seat } },
        });

    private async Task<(HttpClient ManagerClient, Guid TripId)> SeedCompanyTripAsync()
    {
        var (c, t, _) = await SeedCompanyTripWithCompanyAsync();
        return (c, t);
    }

    private async Task<(HttpClient ManagerClient, Guid TripId, Guid CompanyId)> SeedCompanyTripWithCompanyAsync()
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

        var manager = await LoginAsync(managerEmail);
        var bus = await (await PostJson(manager, "/api/vendor/buses",
            new { busNumber = $"C-{Guid.NewGuid():N}".Substring(0, 8), seatCount = 40, type = 0, model = "Bus" }))
            .Content.ReadFromJsonAsync<IdDto>(Json);
        var trip = await (await PostJson(manager, "/api/vendor/trips", new
        {
            busId = bus!.Id,
            origin = "Damascus",
            destination = "Homs",
            departureUtc = DateTimeOffset.UtcNow.AddDays(3),
            arrivalUtc = DateTimeOffset.UtcNow.AddDays(3).AddHours(3),
            price = 60_000m,
            currency = "SYP",
        })).Content.ReadFromJsonAsync<IdDto>(Json);

        return (manager, trip!.Id, companyId);
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

    private static async Task<HttpResponseMessage> PostJson(HttpClient client, string url, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        return await client.SendAsync(req);
    }

    private sealed record AuthDto(string AccessToken, string RefreshToken, string Email);
    private sealed record IdDto(Guid Id);
    private sealed record BookingDto(Guid BookingId, string Reference, decimal TotalAmount, string Currency);
    private sealed record CompanyBookingDto(Guid BookingId, string Reference, string CustomerEmail, string Status);
    private sealed record CancelResult(string Status, bool RefundInitiated);
    private sealed record PagedDto<T>(IReadOnlyList<T> Data, int Total, int Page, int Limit, int TotalPages);
}
