using System.Reflection;
using FluentAssertions;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Domain.Common;

namespace TransportPlatform.UnitTests.Architecture;

/// <summary>
/// Guards the Clean Architecture Dependency Rule at build time: dependencies may only point
/// INWARD (Api → Infrastructure → Application → Domain), never the reverse. These reflection
/// checks fail the moment an inner layer takes a real dependency on an outer one — turning a
/// convention that relied on code review into an automated, non-negotiable guarantee.
/// </summary>
public sealed class LayeringTests
{
    private const string Domain = "TransportPlatform.Domain";
    private const string Application = "TransportPlatform.Application";
    private const string Infrastructure = "TransportPlatform.Infrastructure";
    private const string Api = "TransportPlatform.Api";

    [Fact]
    public void Domain_must_not_depend_on_any_outer_layer()
    {
        // Domain is the core: it must know nothing about use-cases, persistence, or transport.
        var forbidden = new[] { Application, Infrastructure, Api };

        ReferencedTransportAssemblies(typeof(Entity).Assembly)
            .Should().NotIntersectWith(forbidden,
                "the Domain layer must remain free of every outer layer");
    }

    [Fact]
    public void Application_must_not_depend_on_Infrastructure_or_Api()
    {
        // Application orchestrates use-cases against ABSTRACTIONS; it must not reach into the
        // concrete persistence/gateway/HTTP layers (that would invert the dependency rule).
        var forbidden = new[] { Infrastructure, Api };

        ReferencedTransportAssemblies(typeof(IPaymentGateway).Assembly)
            .Should().NotIntersectWith(forbidden,
                "Application handlers must depend on abstractions, not on Infrastructure or Api");
    }

    [Fact]
    public void Application_does_depend_on_Domain()
    {
        // Sanity anchor: proves the reflection actually observes references, so the two
        // "must-not-depend" guards above can't silently pass on an empty list.
        ReferencedTransportAssemblies(typeof(IPaymentGateway).Assembly)
            .Should().Contain(Domain, "use-cases are built on the Domain model");
    }

    /// <summary>The names of the solution's own assemblies that <paramref name="assembly"/> references.</summary>
    private static List<string> ReferencedTransportAssemblies(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => name.StartsWith("TransportPlatform.", StringComparison.Ordinal))
            .ToList();
}
