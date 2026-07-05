using TransportPlatform.Domain.Common;

namespace TransportPlatform.Domain.Bookings;

/// <summary>
/// Graduated cancellation-refund policy: how much of a paid fare is returned depends on how long
/// before departure the customer cancels. Cancelling early is fully refunded; the closer to
/// departure, the smaller the refund — which is standard for transport and stops last-minute
/// cancellations from being cost-free. Tiers are the platform default; making them per-company
/// configurable is a later enhancement.
/// </summary>
public static class RefundPolicy
{
    /// <summary>At/earlier than this before departure → full refund.</summary>
    public static readonly TimeSpan FullRefundBefore = TimeSpan.FromHours(48);

    /// <summary>At/earlier than this (but within the full-refund window) → half refund.</summary>
    public static readonly TimeSpan HalfRefundBefore = TimeSpan.FromHours(24);

    /// <summary>Fraction (0..1) of the fare refunded when cancelling this long before departure.</summary>
    public static decimal RefundFraction(TimeSpan timeToDeparture)
    {
        if (timeToDeparture >= FullRefundBefore) return 1.0m;
        if (timeToDeparture >= HalfRefundBefore) return 0.5m;
        return 0.0m; // under 24h before departure: the seat is freed but no money is returned
    }

    /// <summary>The refundable amount for a paid fare, rounded to 2 decimal places.</summary>
    public static Money RefundableAmount(Money paid, TimeSpan timeToDeparture) =>
        new(Math.Round(paid.Amount * RefundFraction(timeToDeparture), 2, MidpointRounding.ToEven), paid.Currency);
}
