using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Api.Observability;

/// <summary>
/// Wires OpenTelemetry: ASP.NET Core / HttpClient / runtime + custom business metrics, exposed for
/// Prometheus scraping; and request/HTTP tracing exported via OTLP when an endpoint is configured.
/// </summary>
public static class ObservabilityExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration config)
    {
        var serviceName = config["Observability:ServiceName"] ?? "transport-platform-api";
        var otlpEndpoint = config["Observability:Otlp:Endpoint"];

        services.AddSingleton<IAppMetrics, AppMetrics>();

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(AppMetrics.MeterName)
                .AddPrometheusExporter())
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
                // Export traces only when a collector is configured; otherwise they're just sampled
                // and dropped (no noisy export failures in dev / when unconfigured).
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            });

        return services;
    }
}
