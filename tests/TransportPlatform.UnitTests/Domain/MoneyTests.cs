using FluentAssertions;
using TransportPlatform.Domain.Common;

namespace TransportPlatform.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void Negative_amount_is_rejected()
    {
        var act = () => new Money(-1m, "SYP");
        act.Should().Throw<DomainException>().Which.Code.Should().Be("money.negative");
    }

    [Fact]
    public void Currency_must_be_three_letters()
    {
        var act = () => new Money(10m, "DOLLARS");
        act.Should().Throw<DomainException>().Which.Code.Should().Be("money.currency_invalid");
    }

    [Fact]
    public void Multiply_scales_the_amount()
    {
        var fare = new Money(50_000m, "SYP");
        fare.Multiply(3).Amount.Should().Be(150_000m);
    }

    [Fact]
    public void Adding_different_currencies_is_rejected()
    {
        var act = () => new Money(1m, "SYP").Add(new Money(1m, "USD"));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("money.currency_mismatch");
    }
}
