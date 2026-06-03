using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Application.Bookings;
using TransportPlatform.Application.Companies;
using TransportPlatform.Application.Fleet;
using TransportPlatform.Application.Identity;
using TransportPlatform.Application.Notifications;
using TransportPlatform.Application.Payments;
using TransportPlatform.Application.Promotions;
using TransportPlatform.Application.Reports;
using TransportPlatform.Application.Reviews;
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
        services.AddScoped<CounterBookingHandler>();
        services.AddScoped<CancelCompanyBookingHandler>();
        services.AddScoped<ListCompanyBookingsHandler>();
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
        services.AddScoped<ChangePasswordHandler>();
        services.AddScoped<GetProfileHandler>();
        services.AddScoped<UpdateProfileHandler>();

        // Admin · companies
        services.AddScoped<CreateCompanyHandler>();
        services.AddScoped<ListCompaniesHandler>();
        services.AddScoped<SetCompanyStatusHandler>();
        services.AddScoped<CreateCompanyManagerHandler>();
        services.AddScoped<UpdateCompanyHandler>();
        services.AddScoped<DeleteCompanyHandler>();

        // Vendor · company profile
        services.AddScoped<GetMyCompanyHandler>();
        services.AddScoped<UpdateMyCompanyHandler>();

        // Public company directory (trip-search company filter)
        services.AddScoped<ListPublicCompaniesHandler>();

        // Vendor · fleet + trips
        services.AddScoped<AddBusHandler>();
        services.AddScoped<ListBusesHandler>();
        services.AddScoped<UpdateBusHandler>();
        services.AddScoped<DeleteBusHandler>();
        services.AddScoped<ScheduleTripHandler>();
        services.AddScoped<ListVendorTripsHandler>();
        services.AddScoped<CancelTripHandler>();
        services.AddScoped<UpdateTripHandler>();
        services.AddScoped<DeleteTripHandler>();
        services.AddScoped<StartTripHandler>();
        services.AddScoped<CompleteTripHandler>();
        services.AddScoped<RevertTripHandler>();
        services.AddScoped<GetSeatMapHandler>();
        services.AddScoped<SetTripStopsHandler>();
        services.AddScoped<ListTripStopsHandler>();

        // Vendor · staff + drivers
        services.AddScoped<CreateStaffHandler>();
        services.AddScoped<ListStaffHandler>();
        services.AddScoped<SetStaffSuspendedHandler>();
        services.AddScoped<UpdateStaffHandler>();
        services.AddScoped<DeleteStaffHandler>();
        services.AddScoped<AddDriverHandler>();
        services.AddScoped<ListDriversHandler>();
        services.AddScoped<AssignDriverHandler>();

        // Notifications
        services.AddScoped<ListNotificationsHandler>();
        services.AddScoped<UnreadCountHandler>();
        services.AddScoped<MarkNotificationReadHandler>();
        services.AddScoped<MarkAllNotificationsReadHandler>();
        services.AddScoped<DeleteNotificationHandler>();
        services.AddScoped<NotifyCompanyHandler>();

        // Reports + demand
        services.AddScoped<VendorReportSummaryHandler>();
        services.AddScoped<VendorTripReportHandler>();
        services.AddScoped<AdminSystemSummaryHandler>();
        services.AddScoped<PredictDemandHandler>();

        // Reviews
        services.AddScoped<CreateReviewHandler>();
        services.AddScoped<ListTripReviewsHandler>();
        services.AddScoped<ListCompanyReviewsHandler>();

        // Promotions
        services.AddScoped<CreatePromoCodeHandler>();
        services.AddScoped<ListPromoCodesHandler>();
        services.AddScoped<DeactivatePromoCodeHandler>();
        services.AddScoped<PreviewPromoHandler>();

        return services;
    }
}
