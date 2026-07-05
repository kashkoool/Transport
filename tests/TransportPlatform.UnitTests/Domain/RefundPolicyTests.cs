using FluentAssertions;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Common;

namespace TransportPlatform.UnitTests.Domain;

/// <summary>Graduated cancellation-refund tiers by time-to-departure.</summary>
public class RefundPolicyTests
{
    private static readonly Money Paid = new(100_000m, "SYP");

    [Theory]
    [InlineData(72, 100_000)] // ≥48h → full
    [InlineData(48, 100_000)] // exactly 48h → full
    [InlineData(36, 50_000)]  // 24–48h → half
    [InlineData(24, 50_000)]  // exactly 24h → half
    [InlineData(5, 0)]        // <24h → nothing (seat still freed by the handler)
    public void Refund_is_tiered_by_hours_before_departure(int hoursOut, decimal expected)
    {
        var refundable = RefundPolicy.RefundableAmount(Paid, TimeSpan.FromHours(hoursOut));
        refundable.Amount.Should().Be(expected);
        refundable.Currency.Should().Be("SYP");
    }

    [Fact]
    public void Full_refund_fraction_is_one_well_before_departure()
    {
        RefundPolicy.RefundFraction(TimeSpan.FromDays(3)).Should().Be(1.0m);
    }
}
