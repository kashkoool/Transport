using System.Globalization;
using System.IO.Compression;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Scalar.AspNetCore;
using Serilog;
using TransportPlatform.Api.Endpoints;
using TransportPlatform.Api.Identity;
using TransportPlatform.Api.Middleware;
using TransportPlatform.Api.Security;
using TransportPlatform.Api.Workers;
using TransportPlatform.Application;
using TransportPlatform.Application.Common;
using TransportPlatform.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Fail fast in production on weak/placeholder secrets rather than booting insecurely.
StartupGuards.ValidateConfiguration(builder.Configuration, builder.Environment);

// ── Structured logging (Serilog) ───────────────────────────────────────────────
// CA1305: the Console sink's IFormatProvider is supplied (InvariantCulture); the analyzer
// still flags the multi-overload extension method, so it is suppressed for this call only.
#pragma warning disable CA1305
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));
#pragma warning restore CA1305

// ── Real client IP behind a reverse proxy (Nginx/Cloudflare/ALB) ────────────────
// Without this, every request appears to come from the proxy IP and per-IP rate limits
// collapse into one shared global bucket. Hop count is configurable per environment.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = builder.Configuration.GetValue<int?>("Proxy:TrustedHops") ?? 0;
    // KnownNetworks/KnownProxies are cleared only when a hop count is trusted, so a direct
    // attacker cannot spoof X-Forwarded-For in environments that don't sit behind a proxy.
    if (options.ForwardLimit > 0)
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
});

// ── Application + infrastructure (use-cases, EF Core, identity, payment gateway) ─
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Resolve the authenticated caller from request claims, for tenant-scoped handlers.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// ── API surface ────────────────────────────────────────────────────────────────
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();

// ── Response compression (gzip/brotli) ──────────────────────────────────────────
// Cuts JSON payloads ~70-85%. Safe here: short-lived (15-min) bearer tokens are the only
// secrets in any body and no attacker-controlled input is reflected alongside them, so the
// BREACH oracle does not apply. (When the web app moves tokens to HttpOnly cookies, bodies
// carry no secrets at all.)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// ── App-level request timeout (defense-in-depth against a hung request/upstream) ──
builder.Services.AddRequestTimeouts(options =>
    options.DefaultPolicy = new() { Timeout = TimeSpan.FromSeconds(30) });

// AuthN/AuthZ (JWT bearer + authorization) are registered inside AddInfrastructure.

// ── CORS: locked to configured origins (never "*") ──────────────────────────────
const string corsPolicy = "DefaultCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(corsPolicy, policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()
        .SetPreflightMaxAge(TimeSpan.FromHours(24)))); // cache OPTIONS preflight for a day

// ── Rate limiting ───────────────────────────────────────────────────────────────
// A global per-IP fallback covers every endpoint; stricter NAMED policies are applied to
// brute-force-sensitive routes (auth) and abuse-sensitive mutations (booking/checkout).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static string ClientKey(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) }));

    // Auth: login / register / refresh — tight, to blunt credential stuffing & brute force.
    options.AddPolicy(RateLimitPolicies.Auth, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));

    // Sensitive writes: booking hold/create, payment checkout.
    options.AddPolicy(RateLimitPolicies.Sensitive, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

// ── Background workers: seat-hold expiry + outbox publisher ──────────────────────
builder.Services.AddHostedService<SeatHoldExpirySweeper>();
builder.Services.AddHostedService<OutboxPublisher>();

var app = builder.Build();

// ── Middleware pipeline (order matters) ─────────────────────────────────────────
app.UseForwardedHeaders();
app.UseResponseCompression();
app.UseRequestTimeouts();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // interactive API docs at /scalar/v1
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// Security headers applied to every response.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Cross-Origin-Opener-Policy"] = "same-origin";
    // API serves JSON only; lock scripting/plugins/framing to nothing.
    headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});

app.UseCors(corsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ────────────────────────────────────────────────────────────────
app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapVendorEndpoints();
app.MapTripEndpoints();
app.MapBookingEndpoints();
app.MapPaymentEndpoints();

app.Run();

// Exposed so the integration-test WebApplicationFactory can boot the real app.
public partial class Program { }
