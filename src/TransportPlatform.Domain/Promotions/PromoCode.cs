using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Promotions;

public enum DiscountType
{
    Percent = 0,
    Fixed = 1,
}

/// <summary>
/// A company's discount code. A booking may redeem it for a percent or fixed amount off the fare.
/// Validity is bounded by an optional expiry and an optional max-redemptions cap.
/// </summary>
public sealed class PromoCode : AggregateRoot
{
    public const int MaxCodeLength = 40;

    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = null!;
    public DiscountType DiscountType { get; private set; }
    public decimal DiscountValue { get; private set; }
    public int? MaxRedemptions { get; private set; }
    public int RedemptionCount { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public bool Active { get; private set; } = true;

    private PromoCode() { } // EF

    public PromoCode(Guid companyId, string code, DiscountType discountType, decimal discountValue,
        int? maxRedemptions, DateTimeOffset? expiresAtUtc)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("promo.company_required", "A promo code must belong to a company.");
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("promo.code_required", "A code is required.");
        if (code.Trim().Length > MaxCodeLength)
            throw new DomainException("promo.code_too_long", $"Code cannot exceed {MaxCodeLength} characters.");
        if (discountValue <= 0)
            throw new DomainException("promo.value_invalid", "Discount value must be greater than zero.");
        if (discountType == DiscountType.Percent && discountValue > 100)
            throw new DomainException("promo.percent_invalid", "A percentage discount cannot exceed 100.");
        if (maxRedemptions is <= 0)
            throw new DomainException("promo.max_invalid", "Max redemptions must be greater than zero.");

        CompanyId = companyId;
        Code = code.Trim().ToUpperInvariant();
        DiscountType = discountType;
        DiscountValue = discountValue;
        MaxRedemptions = maxRedemptions;
        ExpiresAtUtc = expiresAtUtc;
    }

    public bool CanRedeem(DateTimeOffset now) =>
        Active
        && (ExpiresAtUtc is null || ExpiresAtUtc > now)
        && (MaxRedemptions is null || RedemptionCount < MaxRedemptions);

    /// <summary>The discount for a base amount, never more than the amount itself.</summary>
    public decimal ComputeDiscount(decimal baseAmount)
    {
        var discount = DiscountType == DiscountType.Percent
            ? Math.Round(baseAmount * DiscountValue / 100m, 2)
            : DiscountValue;
        return Math.Min(Math.Max(0, discount), baseAmount);
    }

    public void Redeem() => RedemptionCount++;

    public void Deactivate() => Active = false;
}
