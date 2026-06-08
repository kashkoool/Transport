using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payment", t =>
        {
            t.HasCheckConstraint("CK_payment_amount_nonneg", "\"Amount\" >= 0");
            t.HasCheckConstraint("CK_payment_currency_format", "\"Currency\" ~ '^[A-Z]{3}$'");
            t.HasCheckConstraint(
                "CK_payment_status_valid",
                "\"Status\" IN ('Pending', 'Completed', 'Failed', 'Refunded')");
        });
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

        // FK → booking, Restrict: deleting a booking must never silently erase its payment record —
        // financial history has to survive. Bookings are cancelled (a status change), not hard-deleted.
        builder.HasOne<Booking>().WithMany()
            .HasForeignKey(p => p.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
