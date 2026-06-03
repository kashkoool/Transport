using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Promotions;

namespace TransportPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PromoCodeConfiguration : IEntityTypeConfiguration<PromoCode>
{
    public void Configure(EntityTypeBuilder<PromoCode> builder)
    {
        builder.ToTable("promo_code");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Code).HasMaxLength(PromoCode.MaxCodeLength).IsRequired();
        builder.Property(p => p.DiscountType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.DiscountValue).HasColumnType("numeric(12,2)").IsRequired();

        // A code is unique within a company; lookup at redemption is by (company, code).
        builder.HasIndex(p => new { p.CompanyId, p.Code }).IsUnique();

        builder.HasOne<Company>().WithMany()
            .HasForeignKey(p => p.CompanyId)
            .OnDelete(DeleteBehavior.Cascade); // a company's codes go when the company does
    }
}
