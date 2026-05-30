namespace TransportPlatform.Domain.Payments;

/// <summary>Status of a payment as reported by the external gateway.</summary>
public enum PaymentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Refunded = 3,
}
