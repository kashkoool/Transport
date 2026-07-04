using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Promotions;

namespace TransportPlatform.Application.Promotions;

public sealed record PromoCodeDto(
    Guid Id, string Code, string DiscountType, decimal DiscountValue,
    int? MaxRedemptions, int RedemptionCount, DateTimeOffset? ExpiresAtUtc, bool Active)
{
    public static PromoCodeDto From(PromoCode p) =>
        new(p.Id, p.Code, p.DiscountType.ToString(), p.DiscountValue,
            p.MaxRedemptions, p.RedemptionCount, p.ExpiresAtUtc, p.Active);
}

public sealed record CreatePromoCodeCommand(
    string Code, DiscountType DiscountType, decimal DiscountValue, int? MaxRedemptions, DateTimeOffset? ExpiresAtUtc);

public sealed class CreatePromoCodeValidator : AbstractValidator<CreatePromoCodeCommand>
{
    public CreatePromoCodeValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(PromoCode.MaxCodeLength);
        RuleFor(x => x.DiscountType).IsInEnum();
        RuleFor(x => x.DiscountValue).GreaterThan(0);
        RuleFor(x => x.DiscountValue).LessThanOrEqualTo(100)
            .When(x => x.DiscountType == DiscountType.Percent)
            .WithMessage("A percentage discount cannot exceed 100.");
        // A FIXED-amount discount was previously only lower-bounded; cap it to the same money
        // ceiling as a trip fare so an absurd value can't be stored.
        RuleFor(x => x.DiscountValue).LessThanOrEqualTo(9_999_999.99m)
            .When(x => x.DiscountType == DiscountType.Fixed)
            .WithMessage("A fixed discount is out of range.");
        RuleFor(x => x.MaxRedemptions).InclusiveBetween(1, 1_000_000).When(x => x.MaxRedemptions.HasValue);
    }
}

/// <summary>A vendor manager creates a promo code for their own company.</summary>
public sealed class CreatePromoCodeHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<PromoCodeDto> HandleAsync(CreatePromoCodeCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var promo = new PromoCode(companyId, command.Code, command.DiscountType, command.DiscountValue,
            command.MaxRedemptions, command.ExpiresAtUtc);
        db.PromoCodes.Add(promo);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("promo.code_taken", "A promo code with this name already exists.");
        }
        return PromoCodeDto.From(promo);
    }
}

public sealed record ListPromoCodesQuery(int? Page, int? Limit);

public sealed class ListPromoCodesHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<PagedResult<PromoCodeDto>> HandleAsync(ListPromoCodesQuery query, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var page = new PageRequest(query.Page, query.Limit);
        var codes = db.PromoCodes.Where(p => p.CompanyId == companyId);

        var total = await codes.CountAsync(ct);
        var items = await codes
            .OrderByDescending(p => p.CreatedAtUtc)
            .ThenByDescending(p => p.Id)
            .Skip(page.Skip)
            .Take(page.Limit)
            .Select(p => new PromoCodeDto(p.Id, p.Code, p.DiscountType.ToString(), p.DiscountValue,
                p.MaxRedemptions, p.RedemptionCount, p.ExpiresAtUtc, p.Active))
            .ToListAsync(ct);

        return new PagedResult<PromoCodeDto>(items, total, page.Page, page.Limit);
    }
}

public sealed record DeactivatePromoCodeCommand(Guid PromoCodeId);

public sealed class DeactivatePromoCodeHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task HandleAsync(DeactivatePromoCodeCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var promo = await db.PromoCodes.FirstOrDefaultAsync(p => p.Id == command.PromoCodeId && p.CompanyId == companyId, ct)
            ?? throw new NotFoundException("PromoCode", command.PromoCodeId);
        promo.Deactivate();
        await db.SaveChangesAsync(ct);
    }
}
