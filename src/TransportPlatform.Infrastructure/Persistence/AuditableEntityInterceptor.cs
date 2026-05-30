using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Common;

namespace TransportPlatform.Infrastructure.Persistence;

/// <summary>
/// Stamps CreatedAt/UpdatedAt on every auditable entity at save time, so business code
/// never has to. Uses the abstracted <see cref="IClock"/> for testability.
/// </summary>
public sealed class AuditableEntityInterceptor(IClock clock) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is not null)
        {
            var now = clock.UtcNow;
            foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
            {
                if (entry.State == EntityState.Added)
                    entry.Entity.CreatedAtUtc = now;
                if (entry.State is EntityState.Added or EntityState.Modified)
                    entry.Entity.UpdatedAtUtc = now;
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
