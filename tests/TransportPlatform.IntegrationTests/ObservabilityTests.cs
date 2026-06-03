using System.Net;
using FluentAssertions;

namespace TransportPlatform.IntegrationTests;

/// <summary>The Prometheus scrape endpoint is wired and serves metrics (OpenTelemetry boots OK).</summary>
public sealed class ObservabilityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Metrics_endpoint_is_exposed_for_prometheus()
    {
        var client = factory.CreateClient();

        // Generate some traffic so the ASP.NET Core instrumentation has metrics to emit.
        await client.GetAsync("/health");

        var resp = await client.GetAsync("/metrics");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();
    }
}
