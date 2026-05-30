using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TransportPlatform.Infrastructure.Persistence;

/// <summary>
/// Used only by the EF Core CLI (migrations add / database update) at design time.
/// It builds the DbContext directly — without booting the web host — so migration
/// tooling never depends on the API project's runtime configuration. The connection
/// string here is a design-time placeholder; real migrations run against the configured DB.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=transport;Username=transport;Password=design_time_only";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
