namespace TransportPlatform.Application.Abstractions;

/// <summary>
/// Business metrics emitted by use-cases (implemented over System.Diagnostics.Metrics in the API,
/// exported via OpenTelemetry). Kept as an Application abstraction so handlers don't depend on the
/// metrics transport.
/// </summary>
public interface IAppMetrics
{
    void PaymentSucceeded();
    void PaymentFailed();
    void BookingConfirmed();
}
