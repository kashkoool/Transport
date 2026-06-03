using Microsoft.EntityFrameworkCore;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Fleet;
using TransportPlatform.Domain.Payments;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.Application.Common;

/// <summary>
/// The persistence surface the application layer is allowed to touch. Implemented by the
/// EF Core DbContext in Infrastructure. Keeps handlers testable and free of a concrete DB.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<Bus> Buses { get; }
    DbSet<Driver> Drivers { get; }
    DbSet<Trip> Trips { get; }
    DbSet<SeatHold> SeatHolds { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<SeatAssignment> SeatAssignments { get; }
    DbSet<Payment> Payments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Run <paramref name="action"/> inside a DB transaction using the provider's execution
    /// strategy (retries on transient failures). Used by the booking and payment flows where
    /// several writes must commit atomically.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
