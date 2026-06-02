using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Application.Bookings;
using TransportPlatform.Application.Companies;
using TransportPlatform.Application.Fleet;
using TransportPlatform.Application.Identity;
using TransportPlatform.Application.Payments;
using TransportPlatform.Application.Trips;

namespace TransportPlatform.Application;

/// <summary>Registers application use-case handlers and validators.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Auto-registers every AbstractValidator in this assembly.
        services.AddValidatorsFromAssemblyContaining<HoldSeatsValidator>(ServiceLifetime.Scoped);

        // Booking + payments
        services.AddScoped<SearchTripsHandler>();
        services.AddScoped<HoldSeatsHandler>();
        services.AddScoped<CreateBookingHandler>();
        services.AddScoped<GetTicketHandler>();
        services.AddScoped<ListMyBookingsHandler>();
        services.AddScoped<StartCheckoutHandler>();
        services.AddScoped<ProcessPaymentWebhookHandler>();

        // Auth
        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<RequestPasswordResetHandler>();
        services.AddScoped<ResetPasswordHandler>();
        services.AddScoped<VerifyEmailHandler>();
        services.AddScoped<ResendVerificationHandler>();

        // Admin · companies
        services.AddScoped<CreateCompanyHandler>();
        services.AddScoped<ListCompaniesHandler>();
        services.AddScoped<SetCompanyStatusHandler>();
        services.AddScoped<CreateCompanyManagerHandler>();

        // Vendor · fleet + trips
        services.AddScoped<AddBusHandler>();
        services.AddScoped<ListBusesHandler>();
        services.AddScoped<ScheduleTripHandler>();
        services.AddScoped<ListVendorTripsHandler>();
        services.AddScoped<CancelTripHandler>();

        return services;
    }
}
