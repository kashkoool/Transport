using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Promotions;

namespace TransportPlatform.Application.Promotions;

public sealed record PreviewPromoQuery(Guid TripId, string Code, int Seats);

public sealed record PromoPreview(string Code, decimal OriginalTotal, decimal Discount, decimal Total, string Currency);

public sealed class PreviewPromoValidator : AbstractValidator<PreviewPromoQuery>
{
    public PreviewPromoValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty();
        // Match the domain's trimmed-length contract (PromoCode trims before checking length), so a
        // code valid after trimming isn't rejected for leading/trailing whitespace.
        RuleFor(x => x.Code).Must(c => (c ?? string.Empty).Trim().Length <= PromoCode.MaxCodeLength)
            .WithMessage($"Code cannot exceed {PromoCode.MaxCodeLength} characters.");
        RuleFor(x => x.Seats).InclusiveBetween(1, 10);
    }
}

/// <summary>
/// Customer-facing preview of a promo code against a trip + seat count — validates the code (for the
/// trip's company) and returns the discounted total WITHOUT redeeming it.
/// </summary>
public sealed class PreviewPromoHandler(IApplicationDbContext db, IClock clock)
{
    public async Task<PromoPreview> HandleAsync(PreviewPromoQuery query, CancellationToken ct)
    {
        var seats = Math.Clamp(query.Seats, 1, 10);
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == query.TripId, ct)
            ?? throw new NotFoundException("Trip", query.TripId);

        var originalTotal = trip.Price * seats;
        var (_, discount) = await PromoEvaluation.EvaluateAsync(db, trip.CompanyId, query.Code, originalTotal, clock.UtcNow, ct);
        return new PromoPreview(query.Code.Trim().ToUpperInvariant(), originalTotal, discount, originalTotal - discount, trip.Currency);
    }
}
