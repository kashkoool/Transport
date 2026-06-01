using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Identity;
using TransportPlatform.Infrastructure.Identity;
using TransportPlatform.Infrastructure.Payments;
using TransportPlatform.Infrastructure.Persistence;
using TransportPlatform.Infrastructure.Services;

namespace TransportPlatform.Infrastructure;

public static class DependencyInjection
{
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
            opts.UseNpgsql(cs, npgsql => npgsql.EnableRetryOnFailure());
            opts.AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<DomainEventsToOutboxInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequiredLength = 10;
                o.Password.RequireNonAlphanumeric = true;
                o.User.RequireUniqueEmail = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

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

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IIdentityService, IdentityService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
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
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = "sub",
                };
            });
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.AdminOnly, p =>
                p.RequireRole(UserRoles.Admin, UserRoles.SuperAdmin))
            .AddPolicy(AuthorizationPolicies.VendorOnly, p =>
                p.RequireRole(UserRoles.VendorManager));

        var paymentSection = config.GetSection(PaymentOptions.SectionName);
        services.Configure<PaymentOptions>(options =>
        {
            options.Provider = paymentSection[nameof(PaymentOptions.Provider)] ?? options.Provider;
            options.WebhookSecret = paymentSection[nameof(PaymentOptions.WebhookSecret)] ?? options.WebhookSecret;
            options.CheckoutBaseUrl = paymentSection[nameof(PaymentOptions.CheckoutBaseUrl)] ?? options.CheckoutBaseUrl;
        });
        services.AddSingleton<IPaymentGateway, SandboxPaymentGateway>();
        services.AddSingleton<SandboxPaymentGateway>(); // for tests that need to sign payloads

        return services;
    }
}
