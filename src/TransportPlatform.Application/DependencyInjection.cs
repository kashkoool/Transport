using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Application.Bookings;
using TransportPlatform.Application.Identity;
using TransportPlatform.Application.Payments;
using TransportPlatform.Application.Trips;

namespace TransportPlatform.Application;

/// <summary>Registers application use-case handlers and validators.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<HoldSeatsValidator>(ServiceLifetime.Scoped);

        services.AddScoped<SearchTripsHandler>();
        services.AddScoped<HoldSeatsHandler>();
        services.AddScoped<CreateBookingHandler>();
        services.AddScoped<GetTicketHandler>();
        services.AddScoped<StartCheckoutHandler>();
        services.AddScoped<ProcessPaymentWebhookHandler>();

        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();

        return services;
    }
}
