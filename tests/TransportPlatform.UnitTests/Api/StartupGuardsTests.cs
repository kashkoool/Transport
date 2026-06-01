using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TransportPlatform.Api.Security;

namespace TransportPlatform.UnitTests.Api;

/// <summary>
/// Locks in the production fail-fast guard: a deploy carrying the committed dev placeholders
/// (or a wildcard host, missing proxy hops, non-TLS DB) must NOT be allowed to boot. This is
/// the safety net that stops a misconfigured secret from silently reaching production.
/// </summary>
public class StartupGuardsTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static HostEnv Env(string name) => new(name);

    /// <summary>A fully valid production configuration — the guard must pass.</summary>
    private static Dictionary<string, string?> ValidProd() => new()
    {
        ["Jwt:SigningKey"] = "a-real-32-plus-character-production-signing-key-value",
        ["ConnectionStrings:Postgres"] = "Host=db;Database=transport;Username=app;Password=s3cr3t;SSL Mode=Require",
        ["Payments:WebhookSecret"] = "a-real-production-webhook-secret",
        ["Proxy:TrustedHops"] = "2",
        ["AllowedHosts"] = "api.transport.example",
    };

    [Fact]
    public void Non_production_environments_skip_the_guard()
    {
        // The committed dev placeholders must be fine in Development.
        var act = () => StartupGuards.ValidateConfiguration(Config(new()), Env("Development"));
        act.Should().NotThrow();
    }

    [Fact]
    public void A_fully_valid_production_config_passes()
    {
        var act = () => StartupGuards.ValidateConfiguration(Config(ValidProd()), Env("Production"));
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("Jwt:SigningKey", "dev_only_signing_key_change_me_at_least_32_chars_long")]
    [InlineData("Jwt:SigningKey", "too_short")]
    [InlineData("ConnectionStrings:Postgres", "Host=db;Password=change_me_local_only;SSL Mode=Require")]
    [InlineData("ConnectionStrings:Postgres", "Host=db;Password=real")] // no SSL Mode
    [InlineData("Payments:WebhookSecret", "dev_sandbox_webhook_secret")]
    [InlineData("AllowedHosts", "*")]
    [InlineData("Proxy:TrustedHops", "0")]
    public void Each_placeholder_or_unsafe_value_blocks_production_boot(string key, string badValue)
    {
        var values = ValidProd();
        values[key] = badValue;

        var act = () => StartupGuards.ValidateConfiguration(Config(values), Env("Production"));

        act.Should().Throw<InvalidOperationException>();
    }

    private sealed class HostEnv(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "TransportPlatform.Api";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
