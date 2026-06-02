using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Domain.Outbox;

namespace TransportPlatform.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_message");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Type).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.Error).HasMaxLength(2000);

        // The publisher polls for unprocessed messages oldest-first:
        //   WHERE "ProcessedAtUtc" IS NULL ORDER BY "OccurredAtUtc".
        // A partial index over only the pending rows is far smaller than a full composite index
        // (processed rows — the vast majority over time — are excluded) and matches the predicate
        // exactly. Column quoted because EF/Npgsql creates it case-sensitively.
        builder.HasIndex(m => m.OccurredAtUtc)
            .HasFilter("\"ProcessedAtUtc\" IS NULL");
    }
}
