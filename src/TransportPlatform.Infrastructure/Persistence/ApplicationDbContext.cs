using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
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
