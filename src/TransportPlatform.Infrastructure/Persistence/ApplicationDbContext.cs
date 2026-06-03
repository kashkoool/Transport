using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Fleet;
using TransportPlatform.Domain.Identity;
using TransportPlatform.Domain.Outbox;
using TransportPlatform.Domain.Payments;
using TransportPlatform.Domain.Trips;
using TransportPlatform.Infrastructure.Identity;

namespace TransportPlatform.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context: domain tables + ASP.NET Identity tables. Implements
/// <see cref="IApplicationDbContext"/> so the application layer never sees EF directly.
/// </summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IApplicationDbContext
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Bus> Buses => Set<Bus>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<SeatHold> SeatHolds => Set<SeatHold>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<SeatAssignment> SeatAssignments => Set<SeatAssignment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity tables
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Domain aggregates assign their own Guid identity in the constructor (see Entity.Id).
        // Mark those keys as NOT store-generated. Otherwise EF assumes the database generates
        // them, and a *new* child attached to an *already-tracked* aggregate — e.g. a
        // SeatAssignment created during booking confirmation, where the Booking was loaded
        // first — is mistaken for an existing row and issued as an UPDATE, which affects 0 rows
        // and throws DbUpdateConcurrencyException. ValueGeneratedNever makes EF treat such
        // graph-discovered entities as inserts. No schema change: the columns have no DB default.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(Entity).IsAssignableFrom(entityType.ClrType))
                continue;
            var idProperty = entityType.FindPrimaryKey()?.Properties
                .SingleOrDefault(p => p.Name == nameof(Entity.Id) && p.ClrType == typeof(Guid));
            if (idProperty is not null)
                idProperty.ValueGenerated = ValueGenerated.Never;
        }
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        var strategy = Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}
