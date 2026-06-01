using System.Net;
using FluentAssertions;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// Verifies the split health endpoints: liveness must be dependency-free, readiness must
/// reflect database connectivity.
/// </summary>
public sealed class HealthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Liveness_returns_ok_with_no_dependency_checks()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_returns_ok_when_the_database_is_reachable()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/health/ready");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
