using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
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
