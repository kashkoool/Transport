using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TransportPlatform.Application.Abstractions;
using Testcontainers.PostgreSql;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Fleet;
using TransportPlatform.Domain.Trips;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL container (Testcontainers), so
/// integration tests exercise the genuine EF Core mappings, constraints and transactions
/// — including the unique indexes that guarantee no overbooking.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    /// <summary>Captures every email the app "sends" so tests can assert + extract tokens.</summary>
    public CapturingEmailSender Emails { get; } = new();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Apply migrations (not EnsureCreated) so the test schema is exactly what production
        // builds — this catches migration/model drift and exercises the FK constraints.
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());

        // The API's Jwt:SigningKey lives in appsettings.Development.json, which is not present
        // at the test host's content root, so supply the auth settings explicitly here.
        builder.UseSetting("Jwt:Issuer", "TransportPlatform");
        builder.UseSetting("Jwt:Audience", "TransportPlatform");
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-please-change-0123456789");
        builder.UseSetting("Jwt:AccessTokenMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenDays", "14");

        // Tests boot in Development (so StartupGuards relaxes); disable demo-data seeding so it
        // never pollutes the clean per-test database.
        builder.UseSetting("DevSeed:Enabled", "false");
        // Disable output caching in tests so mutate-then-read assertions see fresh data.
        builder.UseSetting("OutputCache:Enabled", "false");

        // TestServer has no real client IP, so every request keys into the same rate-limit
        // bucket. Raise the limits well above any single test's call count so we exercise the
        // auth/booking FLOW, not the limiter (production keeps the strict config defaults).
        builder.UseSetting("RateLimiting:GlobalPerMinute", "100000");
        builder.UseSetting("RateLimiting:AuthPerMinute", "100000");
        builder.UseSetting("RateLimiting:SensitivePerMinute", "100000");
        builder.UseSetting("RateLimiting:WebhookPerMinute", "100000");
        builder.UseSetting("RateLimiting:PublicReadPerMinute", "100000");
        builder.UseSetting("RateLimiting:ExportPerMinute", "100000");

        // Capture outgoing email so tests can assert on it and extract verify/reset tokens.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    /// <summary>Seed a bookable trip (departs in 2 days) and return its id, seat count and price.</summary>
    public Task<(Guid TripId, int SeatCount, decimal Price)> SeedTripAsync(int seats = 40) =>
        SeedTripAsync(TimeSpan.FromDays(2), seats);

    /// <summary>Seed a bookable trip departing <paramref name="departureFromNow"/> from now.</summary>
    public async Task<(Guid TripId, int SeatCount, decimal Price)> SeedTripAsync(TimeSpan departureFromNow, int seats = 40)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Unique email per call: the fixture is shared across a test class, and the company
        // email is uniquely indexed, so a fixed address collides when a class seeds twice.
        var company = new Company("Test Lines", $"ops-{Guid.NewGuid():N}@testlines.example", "+963000000");
        company.Activate();
        var bus = new Bus(company.Id, $"BUS-{Guid.NewGuid():N}"[..10], seats, BusType.Standard);
        var departure = DateTimeOffset.UtcNow.Add(departureFromNow);
        var trip = new Trip(
            company.Id, bus.Id, "Damascus", "Latakia",
            departure, departure.AddHours(4),
            seats, new Money(50_000m, "SYP"));

        db.Companies.Add(company);
        db.Buses.Add(bus);
        db.Trips.Add(trip);
        await db.SaveChangesAsync();

        return (trip.Id, seats, trip.Price);
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Register a fresh customer through the real endpoint and return an HttpClient whose
    /// Authorization header carries their bearer token, plus the email they registered with.
    /// </summary>
    public async Task<(HttpClient Client, string Email)> CreateCustomerClientAsync()
    {
        var email = $"u{Guid.NewGuid():N}@example.com";
        var client = CreateClient();

        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "Str0ng!Passw0rd", fullName = "Test Customer" });
        resp.EnsureSuccessStatusCode();

        var auth = await resp.Content.ReadFromJsonAsync<AuthDto>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, email);
    }

    private sealed record AuthDto(string AccessToken, string RefreshToken, string Email);
}
