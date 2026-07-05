using FluentAssertions;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.UnitTests.Domain;

/// <summary>
/// Guards on the Payment/Refund state machines added with the money-path hardening: a payment can't
/// regress from a terminal state, and a completed refund can't be un-completed.
/// </summary>
public class PaymentAndRefundTests
{
    private static readonly Money Amount = new(50_000m, "SYP");

    private static Payment NewPayment() =>
        new(Guid.NewGuid(), "Sandbox", Amount, Guid.NewGuid().ToString());

    private static Refund NewRefund(Guid paymentId) =>
        new(paymentId, Guid.NewGuid(), Amount, "Customer cancellation", $"refund-{Guid.NewGuid():N}");

    [Fact]
    public void Payment_completes_then_refunds()
    {
        var payment = NewPayment();
        payment.MarkCompleted("TXN-1");
        payment.MarkRefunded();
        payment.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public void Payment_cannot_refund_before_completion()
    {
        var payment = NewPayment(); // Pending
        var act = payment.MarkRefunded;
        act.Should().Throw<DomainException>().Which.Code.Should().Be("payment.invalid_transition");
    }

    [Fact]
    public void Payment_cannot_complete_after_refund()
    {
        var payment = NewPayment();
        payment.MarkCompleted("TXN-1");
        payment.MarkRefunded();
        var act = () => payment.MarkCompleted("TXN-2"); // a late/replayed capture must not resurrect it
        act.Should().Throw<DomainException>().Which.Code.Should().Be("payment.invalid_transition");
    }

    [Fact]
    public void Payment_complete_is_idempotent()
    {
        var payment = NewPayment();
        payment.MarkCompleted("TXN-1");
        payment.MarkCompleted("TXN-1"); // duplicate webhook
        payment.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public void Payment_cannot_fail_once_completed()
    {
        var payment = NewPayment();
        payment.MarkCompleted("TXN-1");
        var act = payment.MarkFailed;
        act.Should().Throw<DomainException>().Which.Code.Should().Be("payment.invalid_transition");
    }

    [Fact]
    public void Refund_can_fail_while_pending()
    {
        var refund = NewRefund(Guid.NewGuid());
        refund.MarkFailed();
        refund.Status.Should().Be(RefundStatus.Failed);
    }

    [Fact]
    public void Completed_refund_cannot_regress_to_failed()
    {
        var refund = NewRefund(Guid.NewGuid());
        refund.MarkCompleted("REF-1");
        var act = refund.MarkFailed;
        act.Should().Throw<DomainException>().Which.Code.Should().Be("refund.invalid_transition");
    }
}
