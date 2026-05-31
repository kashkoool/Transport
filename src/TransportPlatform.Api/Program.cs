using System.Globalization;
using System.Threading.RateLimiting;
using Scalar.AspNetCore;
using Serilog;
using TransportPlatform.Api.Endpoints;
using TransportPlatform.Api.Middleware;
using TransportPlatform.Api.Workers;
using TransportPlatform.Application;
using TransportPlatform.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Structured logging (Serilog) ───────────────────────────────────────────────
// CA1305: the Console sink's IFormatProvider is supplied (InvariantCulture); the analyzer
// still flags the multi-overload extension method, so it is suppressed for this call only.
#pragma warning disable CA1305
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));
#pragma warning restore CA1305

// ── Application + infrastructure (use-cases, EF Core, identity, payment gateway) ─
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── API surface ────────────────────────────────────────────────────────────────
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();

// AuthN/AuthZ (JWT bearer + authorization) are registered inside AddInfrastructure.

// ── CORS: locked to configured origins (never "*") ──────────────────────────────
const string corsPolicy = "DefaultCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(corsPolicy, policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// ── Rate limiting: a sane global limit per client IP (Valkey-backed later) ───────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
            }));
});

// ── Background workers: seat-hold expiry + outbox publisher ──────────────────────
builder.Services.AddHostedService<SeatHoldExpirySweeper>();
builder.Services.AddHostedService<OutboxPublisher>();

var app = builder.Build();

// ── Middleware pipeline (order matters) ─────────────────────────────────────────
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
    await next();
});

app.UseCors(corsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ────────────────────────────────────────────────────────────────
app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapTripEndpoints();
app.MapBookingEndpoints();
app.MapPaymentEndpoints();

app.Run();

// Exposed so the integration-test WebApplicationFactory can boot the real app.
public partial class Program { }
