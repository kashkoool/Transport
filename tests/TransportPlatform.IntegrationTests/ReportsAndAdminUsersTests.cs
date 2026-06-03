using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Identity;
using TransportPlatform.Infrastructure.Identity;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// Phase-19 surfaces: booking + employee reports reflect desk sales, the currency allow-list is
/// enforced, and admin customer management (list/search/suspend/delete with the active-booking guard).
/// </summary>
public sealed class ReportsAndAdminUsersTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Password = "Str0ng!Passw0rd";
    private static readonly DateTimeOffset Base = DateTimeOffset.UtcNow.AddDays(9);

    [Fact]
    public async Task Booking_and_employee_reports_reflect_a_staff_desk_sale()
    {
        var (manager, companyId) = await SeedManagerAsync();
        var (staff, staffEmail) = await CreateStaffClientAsync(companyId);
        var busId = await AddBusAsync(manager);
        var tripId = await ScheduleTripAsync(manager, busId);

        // Staff sells a seat at the desk → a confirmed booking authored by the staff member.
        var sell = await PostJson(staff, "/api/vendor/bookings", new
        {
            tripId,
            customerEmail = $"walkin-{Guid.NewGuid():N}@example.com",
            passengers = new[] { new { firstName = "Walk", lastName = "In", seatNumber = 5 } },
        });
        sell.StatusCode.Should().Be(HttpStatusCode.OK);

        // Booking report lists it.
        var bookings = await manager.GetFromJsonAsync<List<BookingRow>>("/api/vendor/reports/bookings", Json);
        bookings!.Should().Contain(b => b.Status == "Confirmed" && b.Gateway == "Cash");

        // Employee report credits the staff member.
        var employees = await manager.GetFromJsonAsync<List<EmployeeRow>>("/api/vendor/reports/employees", Json);
        employees!.Should().Contain(e => e.Email == staffEmail && e.Bookings >= 1);
    }

    [Fact]
    public async Task Scheduling_a_trip_with_an_unsupported_currency_is_rejected()
    {
        var (manager, _) = await SeedManagerAsync();
        var busId = await AddBusAsync(manager);

        var bad = await PostJson(manager, "/api/vendor/trips", new
        {
            busId,
            origin = "Damascus",
            destination = "Aleppo",
            departureUtc = Base,
            arrivalUtc = Base.AddHours(4),
            price = 70_000m,
            currency = "GBP", // not in the SYP/USD/EUR allow-list
        });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_lists_searches_and_deletes_customers_with_a_booking_guard()
    {
        var admin = await CreateAdminClientAsync();

        // A customer with no bookings → deletable.
        var (freeClient, freeEmail) = await factory.CreateCustomerClientAsync();
        var freeId = await UserIdByEmailAsync(freeEmail);

        var list = await admin.GetFromJsonAsync<PagedDto<CustomerDto>>(
            $"/api/admin/users?search={Uri.EscapeDataString(freeEmail)}", Json);
        list!.Data.Should().ContainSingle().Which.Email.Should().Be(freeEmail);

        (await admin.PostAsync($"/api/admin/users/{freeId}/suspend", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        // Suspension must end the session: the customer's refresh token can no longer mint tokens.
        (await freeClient.PostAsync("/api/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await admin.PostAsync($"/api/admin/users/{freeId}/reactivate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A customer with a confirmed booking → delete blocked (preserve financial history).
        var (manager, companyId) = await SeedManagerAsync();
        var busId = await AddBusAsync(manager);
        var tripId = await ScheduleTripAsync(manager, busId);
        var (_, bookedEmail) = await factory.CreateCustomerClientAsync();
        await PostJson(manager, "/api/vendor/bookings", new
        {
            tripId,
            customerEmail = bookedEmail,
            passengers = new[] { new { firstName = "Has", lastName = "Booking", seatNumber = 6 } },
        });
        var bookedId = await UserIdByEmailAsync(bookedEmail);

        (await admin.DeleteAsync($"/api/admin/users/{bookedId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        // The booking-free customer deletes cleanly.
        (await admin.DeleteAsync($"/api/admin/users/{freeId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        _ = companyId;
    }

    // ── helpers ───────────────────────────────────────────────────────────────────

    private async Task<(HttpClient Manager, Guid CompanyId)> SeedManagerAsync()
    {
        Guid companyId;
        var managerEmail = $"mgr-{Guid.NewGuid():N}@example.com";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var company = new Company("Report Lines", $"v-{Guid.NewGuid():N}@example.com", null);
            company.Activate();
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            companyId = company.Id;
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            await identity.RegisterVendorManagerAsync(managerEmail, Password, "Mgr", companyId);
        }
        return (await LoginAsync(managerEmail), companyId);
    }

    private async Task<(HttpClient Staff, string Email)> CreateStaffClientAsync(Guid companyId)
    {
        var email = $"staff-{Guid.NewGuid():N}@example.com";
        using (var scope = factory.Services.CreateScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            await identity.RegisterStaffAsync(companyId, email, Password, "Desk Staff", StaffType.Employee);
        }
        return (await LoginAsync(email), email);
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var email = $"admin-{Guid.NewGuid():N}@example.com";
        using (var scope = factory.Services.CreateScope())
        {
            var usersMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var rolesMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await rolesMgr.RoleExistsAsync(UserRoles.Admin))
                await rolesMgr.CreateAsync(new IdentityRole<Guid>(UserRoles.Admin));
            var admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            (await usersMgr.CreateAsync(admin, Password)).Succeeded.Should().BeTrue();
            await usersMgr.AddToRoleAsync(admin, UserRoles.Admin);
        }
        return await LoginAsync(email);
    }

    private async Task<Guid> UserIdByEmailAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Users.AsNoTracking().FirstAsync(u => u.Email == email)).Id;
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
            new { busNumber = $"R-{Guid.NewGuid():N}"[..8], seatCount = 40, type = 0, model = "Bus", seatsPerRow = 4 }))
            .Content.ReadFromJsonAsync<IdDto>(Json);
        return bus!.Id;
    }

    private static async Task<Guid> ScheduleTripAsync(HttpClient client, Guid busId)
    {
        var trip = await (await PostJson(client, "/api/vendor/trips", new
        {
            busId, origin = "Damascus", destination = "Aleppo",
            departureUtc = Base, arrivalUtc = Base.AddHours(4), price = 70_000m, currency = "SYP",
        })).Content.ReadFromJsonAsync<IdDto>(Json);
        return trip!.Id;
    }

    private static async Task<HttpResponseMessage> PostJson(HttpClient client, string url, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        return await client.SendAsync(req);
    }

    private sealed record AuthDto(string AccessToken, string RefreshToken, string Email);
    private sealed record IdDto(Guid Id);
    private sealed record BookingRow(Guid BookingId, string Reference, string CustomerEmail, string Status, decimal TotalAmount, string Currency, DateTimeOffset CreatedAtUtc, int PassengerCount, string Gateway);
    private sealed record EmployeeRow(Guid StaffId, string Email, string FullName, int Bookings, decimal Revenue, string Currency);
    private sealed record CustomerDto(Guid Id, string Email, string FullName, bool Suspended);
    private sealed record PagedDto<T>(IReadOnlyList<T> Data, int Total, int Page, int Limit, int TotalPages);
}
