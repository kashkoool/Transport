using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Fleet;
using TransportPlatform.Domain.Identity;
using TransportPlatform.Domain.Trips;
using TransportPlatform.Infrastructure.Email;
using TransportPlatform.Infrastructure.Identity;

namespace TransportPlatform.Infrastructure.Persistence;

/// <summary>
/// Seeds demo data so a fresh Development database is usable end-to-end immediately: one of
/// each role to log in as, an ACTIVE company with a bus, and a few upcoming Damascus→Latakia
/// trips so customer search returns results. Idempotent (bails once seeded) and wired to run
/// ONLY in Development — it must never execute against a real database.
/// </summary>
public sealed class DevDataSeeder(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    IClock clock,
    ILogger<DevDataSeeder> logger)
{
    // Demo credentials — Development only. Documented in the README so you can log straight in.
    private const string Password = "Demo!Passw0rd";
    public const string AdminEmail = "admin@tpx.local";
    public const string VendorEmail = "vendor@tpx.local";
    public const string CustomerEmail = "customer@tpx.local";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        try
        {
            await SeedCoreAsync(ct);
        }
        // CodeQL: intentional catch-all — dev seeder must never break app startup.
        catch (Exception ex)
        {
            // Never let a seeding hiccup crash startup — the API is still useful without demo data.
            LogSeedFailed(logger, ex);
        }
    }

    private async Task SeedCoreAsync(CancellationToken ct)
    {
        if (await db.Companies.AnyAsync(ct))
            return; // already seeded

        foreach (var role in new[]
                 {
                     UserRoles.Admin, UserRoles.SuperAdmin, UserRoles.VendorManager, UserRoles.Customer,
                 })
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole<Guid>(role));
        }

        await EnsureUserAsync(AdminEmail, "Demo Admin", UserRoles.Admin, companyId: null);

        // An active company with a bus and a few upcoming trips.
        var company = new Company("Demo Lines", "ops@demolines.local", "+963000000");
        company.Activate();
        var bus = new Bus(company.Id, "BUS-001", 40, BusType.Standard);

        var now = clock.UtcNow;
        var trips = Enumerable.Range(1, 3).Select(day =>
        {
            var departure = now.AddDays(day).Date.AddHours(8); // 08:00 UTC on each of the next 3 days
            return new Trip(
                company.Id, bus.Id, "Damascus", "Latakia",
                new DateTimeOffset(departure, TimeSpan.Zero),
                new DateTimeOffset(departure.AddHours(4), TimeSpan.Zero),
                bus.SeatCount, new Money(50_000m, "SYP"));
        }).ToList();

        db.Companies.Add(company);
        db.Buses.Add(bus);
        db.Trips.AddRange(trips);
        await db.SaveChangesAsync(ct);

        await EnsureUserAsync(VendorEmail, "Demo Vendor", UserRoles.VendorManager, company.Id);
        await EnsureUserAsync(CustomerEmail, "Demo Customer", UserRoles.Customer, companyId: null);

        LogSeeded(logger, trips.Count, null);
    }

    private async Task EnsureUserAsync(string email, string fullName, string role, Guid? companyId)
    {
        if (await users.FindByEmailAsync(email) is not null)
            return;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true, // demo accounts skip verification (not implemented yet)
            FullName = fullName,
            CompanyId = companyId,
        };
        var result = await users.CreateAsync(user, Password);
        if (!result.Succeeded)
        {
            LogUserFailed(logger, EmailMask.Mask(email), string.Join("; ", result.Errors.Select(e => e.Code)), null);
            return;
        }
        await users.AddToRoleAsync(user, role);
    }

    private static readonly Action<ILogger, int, Exception?> LogSeeded =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1, nameof(LogSeeded)),
            "Seeded demo data: admin/vendor/customer accounts + Demo Lines with {TripCount} upcoming trips.");

    private static readonly Action<ILogger, string, string, Exception?> LogUserFailed =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(2, nameof(LogUserFailed)),
            "Demo user {Email} not created ({Errors}).");

    private static readonly Action<ILogger, Exception?> LogSeedFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(3, nameof(LogSeedFailed)),
            "Demo data seeding skipped (is the database migrated and reachable?).");
}
