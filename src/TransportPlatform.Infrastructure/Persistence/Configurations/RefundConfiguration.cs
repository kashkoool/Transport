using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.Infrastructure.Persistence.Configurations;

internal sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("refund");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Amount).HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(r => r.Currency).HasMaxLength(3).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.GatewayRefundRef).HasMaxLength(200);
        builder.Property(r => r.Reason).HasMaxLength(500).IsRequired();
        builder.Property(r => r.IdempotencyKey).HasMaxLength(100).IsRequired();

        builder.HasIndex(r => r.PaymentId);
        builder.HasIndex(r => r.BookingId);
        builder.HasIndex(r => r.IdempotencyKey).IsUnique(); // a retry can't double-refund
        builder.HasIndex(r => r.GatewayRefundRef);

        // FK → payment (no navigation). Cascade: a refund is meaningless without its payment.
        builder.HasOne<Payment>().WithMany()
            .HasForeignKey(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
