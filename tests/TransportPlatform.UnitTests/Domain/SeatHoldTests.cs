using FluentAssertions;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Common;

namespace TransportPlatform.UnitTests.Domain;

public class SeatHoldTests
{
    [Fact]
    public void A_fresh_hold_is_active_and_an_expired_one_is_not()
    {
        var now = DateTimeOffset.UtcNow;
        var hold = new SeatHold(Guid.NewGuid(), 5, "user@example.com", now.AddMinutes(10));

        hold.IsActive(now).Should().BeTrue();
        hold.IsActive(now.AddMinutes(11)).Should().BeFalse();
    }

    [Fact]
    public void Consuming_an_expired_hold_is_rejected()
    {
        var now = DateTimeOffset.UtcNow;
        var hold = new SeatHold(Guid.NewGuid(), 5, "user@example.com", now.AddMinutes(-1));

        var act = () => hold.Consume(now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("hold.expired");
    }

    [Fact]
    public void A_hold_cannot_be_consumed_twice()
    {
        var now = DateTimeOffset.UtcNow;
        var hold = new SeatHold(Guid.NewGuid(), 5, "user@example.com", now.AddMinutes(10));

        hold.Consume(now);
        var act = () => hold.Consume(now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("hold.already_consumed");
    }
}
