using System.Diagnostics.Metrics;
using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Api.Observability;

/// <summary>
/// Custom business metrics over <see cref="Meter"/>. Registered with the OpenTelemetry meter
/// provider (see <c>AddMeter(MeterName)</c>) and exported via Prometheus/OTLP.
/// </summary>
public sealed class AppMetrics : IAppMetrics, IDisposable
{
    public const string MeterName = "TransportPlatform";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _paymentsSucceeded;
    private readonly Counter<long> _paymentsFailed;
    private readonly Counter<long> _bookingsConfirmed;

    public AppMetrics()
    {
        _paymentsSucceeded = _meter.CreateCounter<long>("tpx.payments.succeeded", description: "Completed payments.");
        _paymentsFailed = _meter.CreateCounter<long>("tpx.payments.failed", description: "Failed payments.");
        _bookingsConfirmed = _meter.CreateCounter<long>("tpx.bookings.confirmed", description: "Bookings confirmed.");
    }

    public void PaymentSucceeded() => _paymentsSucceeded.Add(1);
    public void PaymentFailed() => _paymentsFailed.Add(1);
    public void BookingConfirmed() => _bookingsConfirmed.Add(1);

    public void Dispose() => _meter.Dispose();
}
