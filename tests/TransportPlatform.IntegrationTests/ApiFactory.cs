using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Each run gets a pristine container, so materialize the current model directly.
        // This still exercises the real mappings + unique indexes that enforce no-overbooking.
        await db.Database.EnsureCreatedAsync();
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
    }

    /// <summary>Seed a bookable trip and return its id, seat count and price.</summary>
    public async Task<(Guid TripId, int SeatCount, decimal Price)> SeedTripAsync(int seats = 40)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Unique email per call: the fixture is shared across a test class, and the company
        // email is uniquely indexed, so a fixed address collides when a class seeds twice.
        var company = new Company("Test Lines", $"ops-{Guid.NewGuid():N}@testlines.example", "+963000000");
        company.Activate();
        var bus = new Bus(company.Id, $"BUS-{Guid.NewGuid():N}"[..10], seats, BusType.Standard);
        var trip = new Trip(
            company.Id, bus.Id, "Damascus", "Latakia",
            DateTimeOffset.UtcNow.AddDays(2), DateTimeOffset.UtcNow.AddDays(2).AddHours(4),
            seats, new Money(50_000m, "SYP"));

        db.Companies.Add(company);
        db.Buses.Add(bus);
        db.Trips.Add(trip);
        await db.SaveChangesAsync();

        return (trip.Id, seats, trip.Price);
    }
}
