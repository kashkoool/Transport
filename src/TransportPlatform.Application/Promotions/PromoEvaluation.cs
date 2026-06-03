using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Promotions;

namespace TransportPlatform.Application.Promotions;

/// <summary>
/// Shared promo validation + discount computation, reused by the booking flow (which then redeems)
/// and the customer-facing preview (which does not). Throws a ConflictException when the code is
/// missing, expired, inactive, fully redeemed, or for another company.
/// </summary>
internal static class PromoEvaluation
{
    public static async Task<(PromoCode Promo, decimal Discount)> EvaluateAsync(
        IApplicationDbContext db, Guid companyId, string code, decimal baseAmount, DateTimeOffset now, CancellationToken ct)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        var promo = await db.PromoCodes.FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Code == normalized, ct);
        if (promo is null || !promo.CanRedeem(now))
            throw new ConflictException("promo.invalid", "This promo code is not valid for this trip.");
        return (promo, promo.ComputeDiscount(baseAmount));
    }
}
