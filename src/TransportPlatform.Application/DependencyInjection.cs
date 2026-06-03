using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Application.Bookings;
using TransportPlatform.Application.Companies;
using TransportPlatform.Application.Fleet;
using TransportPlatform.Application.Identity;
using TransportPlatform.Application.Notifications;
using TransportPlatform.Application.Payments;
using TransportPlatform.Application.Staff;
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
        services.AddScoped<CancelBookingHandler>();
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
        services.AddScoped<GoogleSignInHandler>();

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

        // Vendor · staff + drivers
        services.AddScoped<CreateStaffHandler>();
        services.AddScoped<ListStaffHandler>();
        services.AddScoped<SetStaffSuspendedHandler>();
        services.AddScoped<AddDriverHandler>();
        services.AddScoped<ListDriversHandler>();
        services.AddScoped<AssignDriverHandler>();

        // Notifications
        services.AddScoped<ListNotificationsHandler>();
        services.AddScoped<UnreadCountHandler>();
        services.AddScoped<MarkNotificationReadHandler>();
        services.AddScoped<MarkAllNotificationsReadHandler>();
        services.AddScoped<NotifyCompanyHandler>();

        return services;
    }
}
