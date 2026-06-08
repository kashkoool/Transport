using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.Infrastructure.Persistence.Configurations;

internal sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("refund", t =>
        {
            t.HasCheckConstraint("CK_refund_amount_nonneg", "\"Amount\" >= 0");
            t.HasCheckConstraint("CK_refund_currency_format", "\"Currency\" ~ '^[A-Z]{3}$'");
            t.HasCheckConstraint(
                "CK_refund_status_valid", "\"Status\" IN ('Pending', 'Completed', 'Failed')");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Amount).HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(r => r.Currency).HasMaxLength(3).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.GatewayRefundRef).HasMaxLength(200);
        builder.Property(r => r.Reason).HasMaxLength(500).IsRequired();
        builder.Property(r => r.IdempotencyKey).HasMaxLength(100).IsRequired();

        builder.HasIndex(r => r.PaymentId);
        builder.HasIndex(r => r.IdempotencyKey).IsUnique(); // a retry can't double-refund
        builder.HasIndex(r => r.GatewayRefundRef);

        // FK → payment (no navigation). Restrict: a refund is part of the financial record and must
        // not be erased by deleting its payment.
        builder.HasOne<Payment>().WithMany()
            .HasForeignKey(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK → booking (no navigation). The refund already stores BookingId; back it with a real
        // constraint so it can never reference a missing booking (auto-creates IX_refund_BookingId).
        builder.HasOne<Booking>().WithMany()
            .HasForeignKey(r => r.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
