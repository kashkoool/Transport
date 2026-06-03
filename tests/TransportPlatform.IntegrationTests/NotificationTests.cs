using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Notifications;
using TransportPlatform.Domain.Bookings.Events;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// In-app notifications: a booking-confirmation event creates + delivers a customer notification,
/// read-state mutates correctly, and an admin → company message reaches the company manager.
/// </summary>
public sealed class NotificationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Password = "Str0ng!Passw0rd";

    [Fact]
    public async Task Booking_confirmation_creates_and_delivers_a_customer_notification()
    {
        var (client, email) = await factory.CreateCustomerClientAsync();
        await DispatchAsync(new BookingConfirmedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), "BK-NOTIF1", email));

        var page = await client.GetFromJsonAsync<PagedDto<NotifDto>>("/api/notifications", Json);
        page!.Data.Should().Contain(n => n.Title == "Booking confirmed" && !n.IsRead);

        var unread = await client.GetFromJsonAsync<UnreadDto>("/api/notifications/unread-count", Json);
        unread!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Marking_a_notification_read_clears_it_from_the_unread_count()
    {
        var (client, email) = await factory.CreateCustomerClientAsync();
        await DispatchAsync(new BookingConfirmedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), "BK-NOTIF2", email));

        var page = await client.GetFromJsonAsync<PagedDto<NotifDto>>("/api/notifications", Json);
        var id = page!.Data.Single(n => n.Title == "Booking confirmed").Id;

        (await client.PostAsJsonAsync($"/api/notifications/{id}/read", new { }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var unread = await client.GetFromJsonAsync<UnreadDto>("/api/notifications/unread-count", Json);
        unread!.Count.Should().Be(0);
    }

    [Fact]
    public async Task Admin_company_message_reaches_the_company_manager()
    {
        var (companyId, managerClient) = await SeedCompanyWithManagerAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<NotifyCompanyHandler>();
            var result = await handler.HandleAsync(
                new NotifyCompanyCommand(companyId, "Action required", "Please update your profile.", "warning"), default);
            result.Recipients.Should().BeGreaterThan(0);
        }

        var page = await managerClient.GetFromJsonAsync<PagedDto<NotifDto>>("/api/notifications", Json);
        page!.Data.Should().Contain(n => n.Title == "Action required" && n.Type == "warning");
    }

    // ── helpers ───────────────────────────────────────────────────────────────────

    private async Task DispatchAsync(BookingConfirmedDomainEvent evt)
    {
        using var scope = factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IIntegrationEventDispatcher>();
        await dispatcher.DispatchAsync(typeof(BookingConfirmedDomainEvent).FullName!, JsonSerializer.Serialize(evt));
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
        var auth = await login.Content.ReadFromJsonAsync<AuthDto>(Json);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (companyId, client);
    }

    private sealed record AuthDto(string AccessToken, string RefreshToken, string Email);
    private sealed record NotifDto(Guid Id, string Title, string Message, string Type, bool IsRead, DateTimeOffset CreatedAtUtc);
    private sealed record UnreadDto(int Count);
    private sealed record PagedDto<T>(IReadOnlyList<T> Data, int Total, int Page, int Limit, int TotalPages);
}
