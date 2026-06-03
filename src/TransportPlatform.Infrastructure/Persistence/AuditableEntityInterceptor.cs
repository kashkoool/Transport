using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Common;

namespace TransportPlatform.Infrastructure.Persistence;

/// <summary>
/// Stamps CreatedAt/UpdatedAt — and the creating user — on every auditable entity at save time, so
/// business code never has to. Uses the abstracted <see cref="IClock"/> for testability, and the
/// current principal (when there is one — background/seed writes leave CreatedBy null).
/// </summary>
public sealed class AuditableEntityInterceptor(IClock clock, ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is not null)
        {
            var now = clock.UtcNow;
            var actor = currentUser.UserId?.ToString();
            foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.CreatedBy ??= actor; // record the creator; explicit values win
                }
                if (entry.State is EntityState.Added or EntityState.Modified)
                    entry.Entity.UpdatedAtUtc = now;
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
