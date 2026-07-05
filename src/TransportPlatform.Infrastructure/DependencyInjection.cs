using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Identity;
using TransportPlatform.Infrastructure.Email;
using TransportPlatform.Infrastructure.Identity;
using TransportPlatform.Infrastructure.Payments;
using TransportPlatform.Infrastructure.Persistence;
using TransportPlatform.Infrastructure.Services;

namespace TransportPlatform.Infrastructure;

public static class DependencyInjection
{
    // Pin JWT signing to HS256 (the symmetric alg we issue with) to defend against alg-confusion.
    private static readonly string[] AllowedJwtAlgorithms = ["HS256"];

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IReferenceGenerator, ReferenceGenerator>();

        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<DomainEventsToOutboxInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, opts) =>
        {
            var cs = config.GetConnectionString("Postgres")
                     ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
            // Bound the per-instance pool so (MaxPoolSize x app instances) stays well under Postgres
            // max_connections (default 100), leaving headroom for migrations + admin tools. Tunable per
            // environment via Database:MaxPoolSize; a connection pooler (PgBouncer) is the next step
            // past this ceiling once instance count climbs.
            cs = new NpgsqlConnectionStringBuilder(cs)
            {
                MaxPoolSize = config.GetValue("Database:MaxPoolSize", 20),
            }.ConnectionString;
            opts.UseNpgsql(cs, npgsql =>
            {
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                npgsql.CommandTimeout(30); // cap a runaway query so it can't drain the pool
            });
            opts.AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<DomainEventsToOutboxInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // Development-only demo-data seeder (invoked from Program.cs only when env is Development).
        services.AddScoped<DevDataSeeder>();

        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequiredLength = 10;
                o.Password.RequireNonAlphanumeric = true;
                o.User.RequireUniqueEmail = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders(); // enables password-reset + email-confirmation tokens

        // ── Auth: token + identity services and JWT bearer validation ───────────────
        var jwtSection = config.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(o =>
        {
            o.Issuer = jwtSection[nameof(JwtOptions.Issuer)] ?? o.Issuer;
            o.Audience = jwtSection[nameof(JwtOptions.Audience)] ?? o.Audience;
            o.SigningKey = jwtSection[nameof(JwtOptions.SigningKey)] ?? o.SigningKey;
            if (int.TryParse(jwtSection[nameof(JwtOptions.AccessTokenMinutes)], out var accessMinutes))
                o.AccessTokenMinutes = accessMinutes;
            if (int.TryParse(jwtSection[nameof(JwtOptions.RefreshTokenDays)], out var refreshDays))
                o.RefreshTokenDays = refreshDays;
        });

        var signingKey = jwtSection[nameof(JwtOptions.SigningKey)]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

        // Login policy (e.g. require a verified email). Off unless explicitly enabled in config.
        var authSection = config.GetSection(AuthOptions.SectionName);
        services.Configure<AuthOptions>(o =>
        {
            if (bool.TryParse(authSection[nameof(AuthOptions.RequireEmailVerification)], out var require))
                o.RequireEmailVerification = require;
        });

        // Accepted currencies (single source of truth). Bind from config if present, else default.
        var currencySection = config.GetSection(CurrencyOptions.SectionName);
        services.Configure<CurrencyOptions>(o =>
        {
            var configured = currencySection.GetSection(nameof(CurrencyOptions.Supported)).Get<string[]>();
            if (configured is { Length: > 0 })
                o.Supported = configured;
        });

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IIdentityService, IdentityService>();

        var authBuilder = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
        authBuilder.AddJwtBearer(o =>
            {
                o.MapInboundClaims = false; // keep "sub"/"email" claims literal
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSection[nameof(JwtOptions.Issuer)],
                    ValidateAudience = true,
                    ValidAudience = jwtSection[nameof(JwtOptions.Audience)],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    // Pin the accepted signature algorithm to defend against alg-confusion attacks
                    // (e.g. a token forged with "none" or an asymmetric alg the key isn't meant for).
                    ValidAlgorithms = AllowedJwtAlgorithms,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = "sub",
                };
                // WebSocket clients can't set the Authorization header, so SignalR passes the
                // access token in the query string for hub connections.
                o.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },
                    // Per-request user-state check: without this, a suspended (locked-out) or
                    // deleted user's already-issued access token stays valid until it naturally
                    // expires (AccessTokenMinutes, default 15). Authorization is otherwise decided
                    // purely from JWT claims, so revocation would lag by up to that window. We accept
                    // one DB lookup per authenticated request as the cost of *immediate* revocation:
                    // if the user is gone or locked out, the token is rejected 401 right now. The
                    // refresh path enforces the same lockout re-check (JwtTokenService.RefreshAsync)
                    // so a suspended user can neither use an existing access token nor mint a new one.
                    OnTokenValidated = async context =>
                    {
                        try
                        {
                            var userId = context.Principal?.FindFirst("sub")?.Value;
                            if (string.IsNullOrWhiteSpace(userId))
                            {
                                context.Fail("Token is missing the subject claim.");
                                return;
                            }

                            var userManager = context.HttpContext.RequestServices
                                .GetRequiredService<UserManager<ApplicationUser>>();
                            var user = await userManager.FindByIdAsync(userId);

                            // User deleted, or account suspended (locked out) → reject immediately.
                            if (user is null || await userManager.IsLockedOutAsync(user))
                                context.Fail("The account is no longer active.");
                        }
                        catch
                        {
                            // Fail closed: any lookup error rejects the request rather than letting a
                            // possibly-revoked token through. Never rethrow — that would surface as a 500.
                            context.Fail("Unable to verify the account state.");
                        }
                    },
                };
            });

        // ── Google sign-in (optional: wired only when a client id is configured) ────────
        var googleSection = config.GetSection(GoogleAuthOptions.SectionName);
        services.Configure<GoogleAuthOptions>(o =>
        {
            o.ClientId = googleSection[nameof(GoogleAuthOptions.ClientId)] ?? o.ClientId;
            o.ClientSecret = googleSection[nameof(GoogleAuthOptions.ClientSecret)] ?? o.ClientSecret;
            o.CallbackPath = googleSection[nameof(GoogleAuthOptions.CallbackPath)] ?? o.CallbackPath;
        });
        var googleClientId = googleSection[nameof(GoogleAuthOptions.ClientId)];
        // App-facing flag the API endpoints branch on (no Infrastructure type leaks to the endpoints).
        services.Configure<ExternalAuthOptions>(o => o.GoogleEnabled = !string.IsNullOrWhiteSpace(googleClientId));
        if (!string.IsNullOrWhiteSpace(googleClientId))
        {
            // Short-lived cookie that carries the external identity from Google's callback to our
            // /api/auth/google/callback endpoint, where it's read once and signed out.
            authBuilder.AddCookie(ExternalAuthOptions.ExternalCookieScheme, o =>
            {
                o.Cookie.Name = "tpx.external";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax; // survives the top-level GET redirect from Google
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always; // OAuth is HTTPS-only; never emit over plain HTTP
                o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                o.SlidingExpiration = false;
            });
            authBuilder.AddGoogle(ExternalAuthOptions.GoogleScheme, o =>
            {
                o.ClientId = googleClientId;
                o.ClientSecret = googleSection[nameof(GoogleAuthOptions.ClientSecret)] ?? string.Empty;
                o.CallbackPath = googleSection[nameof(GoogleAuthOptions.CallbackPath)] ?? "/signin-google";
                o.SignInScheme = ExternalAuthOptions.ExternalCookieScheme;
                o.SaveTokens = false; // we don't need Google's access/refresh tokens after sign-in
                // Capture whether Google says the email is verified, so the link/create decision
                // can require a proven email (see IdentityService.FindOrCreateExternalUserAsync).
                o.Events.OnCreatingTicket = context =>
                {
                    if (context.User.TryGetProperty("email_verified", out var verified)
                        && verified.ValueKind == JsonValueKind.True)
                    {
                        context.Identity?.AddClaim(new Claim("email_verified", "true"));
                    }
                    return Task.CompletedTask;
                };
            });
        }

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.AdminOnly, p =>
                p.RequireRole(UserRoles.Admin, UserRoles.SuperAdmin))
            .AddPolicy(AuthorizationPolicies.VendorOnly, p =>
                p.RequireRole(UserRoles.VendorManager))
            .AddPolicy(AuthorizationPolicies.VendorOrStaff, p =>
                p.RequireRole(UserRoles.VendorManager, UserRoles.Staff));

        var paymentSection = config.GetSection(PaymentOptions.SectionName);
        services.Configure<PaymentOptions>(options =>
        {
            options.Provider = paymentSection[nameof(PaymentOptions.Provider)] ?? options.Provider;
            options.WebhookSecret = paymentSection[nameof(PaymentOptions.WebhookSecret)] ?? options.WebhookSecret;
            options.CheckoutBaseUrl = paymentSection[nameof(PaymentOptions.CheckoutBaseUrl)] ?? options.CheckoutBaseUrl;
            options.ClientId = paymentSection[nameof(PaymentOptions.ClientId)] ?? options.ClientId;
            options.ClientSecret = paymentSection[nameof(PaymentOptions.ClientSecret)] ?? options.ClientSecret;
            options.ApiBaseUrl = paymentSection[nameof(PaymentOptions.ApiBaseUrl)] ?? options.ApiBaseUrl;
            options.WebhookId = paymentSection[nameof(PaymentOptions.WebhookId)] ?? options.WebhookId;
            options.ReturnUrl = paymentSection[nameof(PaymentOptions.ReturnUrl)] ?? options.ReturnUrl;
            options.CancelUrl = paymentSection[nameof(PaymentOptions.CancelUrl)] ?? options.CancelUrl;
        });

        // The Sandbox gateway is always available: it backs dev/tests and provides the dev-only
        // webhook signer seam. The active IPaymentGateway is chosen by Payments:Provider.
        services.AddSingleton<SandboxPaymentGateway>();
        services.AddSingleton<IPaymentWebhookSigner>(sp => sp.GetRequiredService<SandboxPaymentGateway>());

        if (string.Equals(paymentSection[nameof(PaymentOptions.Provider)], "PayPal", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient(PayPalPaymentGateway.HttpClientName, client =>
            {
                client.BaseAddress = new Uri(paymentSection[nameof(PaymentOptions.ApiBaseUrl)] ?? "https://api-m.sandbox.paypal.com");
                client.Timeout = TimeSpan.FromSeconds(30); // bound external calls
            })
            // Retry (with backoff+jitter) transient failures + a circuit breaker to shed load during a
            // PayPal outage. All gateway writes carry a PayPal-Request-Id idempotency key, so retries
            // can't double-charge/double-refund.
            .AddStandardResilienceHandler();
            services.AddSingleton<PayPalPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<PayPalPaymentGateway>());
        }
        else
        {
            services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<SandboxPaymentGateway>());
        }

        // ── Email: SMTP when a host is configured, else a dev logging sink ──────────────
        var emailSection = config.GetSection(EmailOptions.SectionName);
        services.Configure<EmailOptions>(o =>
        {
            o.SmtpHost = emailSection[nameof(EmailOptions.SmtpHost)] ?? o.SmtpHost;
            if (int.TryParse(emailSection[nameof(EmailOptions.SmtpPort)], out var port)) o.SmtpPort = port;
            o.SmtpUsername = emailSection[nameof(EmailOptions.SmtpUsername)] ?? o.SmtpUsername;
            o.SmtpPassword = emailSection[nameof(EmailOptions.SmtpPassword)] ?? o.SmtpPassword;
            if (bool.TryParse(emailSection[nameof(EmailOptions.UseStartTls)], out var tls)) o.UseStartTls = tls;
            if (int.TryParse(emailSection[nameof(EmailOptions.SmtpTimeoutSeconds)], out var timeout)) o.SmtpTimeoutSeconds = timeout;
            o.FromAddress = emailSection[nameof(EmailOptions.FromAddress)] ?? o.FromAddress;
            o.FromName = emailSection[nameof(EmailOptions.FromName)] ?? o.FromName;
            o.FrontendBaseUrl = emailSection[nameof(EmailOptions.FrontendBaseUrl)] ?? o.FrontendBaseUrl;
            o.PasswordResetPath = emailSection[nameof(EmailOptions.PasswordResetPath)] ?? o.PasswordResetPath;
            o.VerifyEmailPath = emailSection[nameof(EmailOptions.VerifyEmailPath)] ?? o.VerifyEmailPath;
        });
        if (string.IsNullOrWhiteSpace(emailSection[nameof(EmailOptions.SmtpHost)]))
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        else
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IAuthEmailService, AuthEmailService>();

        // Dispatches outbox events (e.g. booking-confirmed) to side effects like email.
        services.AddScoped<IIntegrationEventDispatcher, OutboxEventDispatcher>();

        // Report exporters (XLSX/PDF). CSV is dependency-free in the Application layer.
        services.AddSingleton<IReportExporter, Reports.ReportExporter>();

        return services;
    }
}
