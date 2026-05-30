using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Outbox;

namespace TransportPlatform.Infrastructure.Persistence;

/// <summary>
/// Collects domain events raised by aggregates and writes them to the outbox table IN THE
/// SAME transaction as the state change (the outbox pattern). A background publisher then
/// delivers them, so events survive a crash that happens right after commit.
/// </summary>
public sealed class DomainEventsToOutboxInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var aggregates = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                var message = new OutboxMessage(
                    type: domainEvent.GetType().FullName!,
                    payload: JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    occurredAtUtc: domainEvent.OccurredAtUtc);
                context.Set<OutboxMessage>().Add(message);
            }

            aggregate.ClearDomainEvents();
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
