using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Domain.Reviews;

namespace TransportPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("review");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.CustomerEmail).HasMaxLength(256).IsRequired();
        builder.Property(r => r.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(r => r.Comment).HasMaxLength(1000);

        builder.HasIndex(r => r.BookingId).IsUnique(); // one review per booking
        builder.HasIndex(r => r.TripId);
        builder.HasIndex(r => r.CompanyId);
        // Soft references (no FK) to trip/company/booking, like notification — avoids cascade churn.
    }
}
