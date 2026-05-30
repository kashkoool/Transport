using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payment");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Gateway).HasMaxLength(50).IsRequired();
        builder.Property(p => p.GatewayTxnRef).HasMaxLength(200);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.Amount).HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.IdempotencyKey).HasMaxLength(100).IsRequired();

        // One payment per booking; idempotency key dedupes retried checkouts.
        builder.HasIndex(p => p.BookingId).IsUnique();
        builder.HasIndex(p => p.IdempotencyKey).IsUnique();
        builder.HasIndex(p => p.GatewayTxnRef);
    }
}
