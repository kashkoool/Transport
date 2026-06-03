namespace TransportPlatform.Domain.Payments;

/// <summary>Lifecycle of a refund request against a completed payment.</summary>
public enum RefundStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
}
