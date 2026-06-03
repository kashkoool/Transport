using System.Text;
using FluentValidation;
using TransportPlatform.Api.Security;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Bookings;
using TransportPlatform.Application.Common;
using TransportPlatform.Application.Companies;
using TransportPlatform.Application.Fleet;
using TransportPlatform.Application.Promotions;
using TransportPlatform.Application.Reports;
using TransportPlatform.Application.Staff;
using TransportPlatform.Application.Trips;
using TransportPlatform.Domain.Fleet;
using TransportPlatform.Domain.Identity;
using TransportPlatform.Domain.Promotions;

namespace TransportPlatform.Api.Endpoints;

public static class VendorEndpoints
{
    public sealed record AddBusRequest(string BusNumber, int SeatCount, BusType Type, string? Model, int? SeatsPerRow);
    public sealed record ScheduleTripRequest(
        Guid BusId, string Origin, string Destination,
        DateTimeOffset DepartureUtc, DateTimeOffset ArrivalUtc, decimal Price, string Currency);
    public sealed record CreateStaffRequest(string Email, string Password, string FullName, StaffType StaffType);
    public sealed record AddDriverRequest(string FullName, string? Phone, string? LicenseNumber);
    public sealed record AssignDriverRequest(Guid? DriverId);
    public sealed record CounterBookingRequest(Guid TripId, IReadOnlyList<PassengerInput> Passengers, string CustomerEmail);
    public sealed record UpdateBusRequest(int SeatCount, BusType Type, string? Model, int? SeatsPerRow);
    public sealed record UpdateTripRequest(
        string Origin, string Destination, DateTimeOffset DepartureUtc, DateTimeOffset ArrivalUtc, decimal Price, string Currency);
    public sealed record SetTripStopsRequest(IReadOnlyList<TripStopInput> Stops);
    public sealed record UpdateCompanyProfileRequest(string Name, string? Phone);
    public sealed record CreatePromoRequest(
        string Code, DiscountType DiscountType, decimal DiscountValue, int? MaxRedemptions, DateTimeOffset? ExpiresAtUtc);

    public static IEndpointRouteBuilder MapVendorEndpoints(this IEndpointRouteBuilder app)
    {
        // Vendor managers only. Every handler scopes to the caller's own company id.
        var group = app.MapGroup("/api/vendor").WithTags("Vendor")
            .RequireAuthorization(AuthorizationPolicies.VendorOnly)
            .RequireRateLimiting(RateLimitPolicies.Sensitive);

        // ── Fleet ───────────────────────────────────────────────────────────────────
        group.MapPost("/buses", async (
            AddBusRequest body, AddBusHandler handler,
            IValidator<AddBusCommand> validator, CancellationToken ct) =>
        {
            var command = new AddBusCommand(body.BusNumber, body.SeatCount, body.Type, body.Model,
                body.SeatsPerRow ?? Bus.DefaultSeatsPerRow);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("AddBus")
        .WithSummary("Add a bus to your fleet.");

        group.MapGet("/buses", async (
            int? page, int? limit, ListBusesHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListBusesQuery(page, limit), ct)))
        .WithName("ListBuses")
        .WithSummary("List your fleet (paginated).");

        // ── Trips ───────────────────────────────────────────────────────────────────
        group.MapPost("/trips", async (
            ScheduleTripRequest body, ScheduleTripHandler handler,
            IValidator<ScheduleTripCommand> validator, CancellationToken ct) =>
        {
            var command = new ScheduleTripCommand(body.BusId, body.Origin, body.Destination,
                body.DepartureUtc, body.ArrivalUtc, body.Price, body.Currency);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("ScheduleTrip")
        .WithSummary("Schedule a trip on one of your buses.");

        group.MapGet("/trips", async (
            int? page, int? limit, ListVendorTripsHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListVendorTripsQuery(page, limit), ct)))
        .WithName("ListVendorTrips")
        .WithSummary("List your trips (paginated).");

        group.MapPost("/trips/{id:guid}/cancel", async (
            Guid id, CancelTripHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new CancelTripCommand(id), ct)))
        .WithName("CancelTrip")
        .WithSummary("Cancel one of your trips.");

        // ── Staff ─────────────────────────────────────────────────────────────────────
        group.MapPost("/staff", async (
            CreateStaffRequest body, CreateStaffHandler handler,
            IValidator<CreateStaffCommand> validator, CancellationToken ct) =>
        {
            var command = new CreateStaffCommand(body.Email, body.Password, body.FullName, body.StaffType);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("CreateStaff")
        .WithSummary("Create a staff account in your company.");

        group.MapGet("/staff", async (
            int? page, int? limit, ListStaffHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListStaffQuery(page, limit), ct)))
        .WithName("ListStaff")
        .WithSummary("List your company staff (paginated).");

        group.MapPost("/staff/{id:guid}/suspend", async (
            Guid id, SetStaffSuspendedHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new SetStaffSuspendedCommand(id, Suspended: true), ct);
            return Results.NoContent();
        })
        .WithName("SuspendStaff")
        .WithSummary("Suspend a staff member (blocks their login).");

        group.MapPost("/staff/{id:guid}/reactivate", async (
            Guid id, SetStaffSuspendedHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new SetStaffSuspendedCommand(id, Suspended: false), ct);
            return Results.NoContent();
        })
        .WithName("ReactivateStaff")
        .WithSummary("Reactivate a suspended staff member.");

        // ── Drivers (no login; assignable to a bus) ─────────────────────────────────────
        group.MapPost("/drivers", async (
            AddDriverRequest body, AddDriverHandler handler,
            IValidator<AddDriverCommand> validator, CancellationToken ct) =>
        {
            var command = new AddDriverCommand(body.FullName, body.Phone, body.LicenseNumber);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("AddDriver")
        .WithSummary("Add a driver to your company.");

        group.MapGet("/drivers", async (
            int? page, int? limit, ListDriversHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListDriversQuery(page, limit), ct)))
        .WithName("ListDrivers")
        .WithSummary("List your drivers (paginated).");

        group.MapPost("/buses/{id:guid}/driver", async (
            Guid id, AssignDriverRequest body, AssignDriverHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new AssignDriverCommand(id, body.DriverId), ct)))
        .WithName("AssignBusDriver")
        .WithSummary("Assign (or clear) the driver of one of your buses.");

        // ── Reports + demand ────────────────────────────────────────────────────────
        group.MapGet("/reports/summary", async (
            DateTimeOffset? from, DateTimeOffset? to, VendorReportSummaryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new VendorReportQuery(from, to), ct)))
        .WithName("VendorReportSummary")
        .WithSummary("Financial + occupancy summary for your company over a date range.");

        group.MapGet("/reports/trips", async (
            DateTimeOffset? from, DateTimeOffset? to, VendorTripReportHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new VendorReportQuery(from, to), ct)))
        .WithName("VendorTripReport")
        .WithSummary("Per-trip occupancy + revenue for your company.");

        group.MapGet("/reports/trips/export", async (
            DateTimeOffset? from, DateTimeOffset? to, string? format,
            VendorTripReportHandler handler, IReportExporter exporter, CancellationToken ct) =>
        {
            var rows = await handler.HandleAsync(new VendorReportQuery(from, to), ct);
            return (format?.ToLowerInvariant()) switch
            {
                "xlsx" => Results.File(exporter.TripsToXlsx(rows),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "trips-report.xlsx"),
                "pdf" => Results.File(exporter.TripsToPdf(rows), "application/pdf", "trips-report.pdf"),
                _ => Results.File(Encoding.UTF8.GetBytes(TripReportCsv.Build(rows)), "text/csv", "trips-report.csv"),
            };
        })
        .WithName("VendorTripReportExport")
        .WithSummary("Download the per-trip report (format=csv|xlsx|pdf, default csv).");

        group.MapGet("/demand/predict", async (
            string origin, string destination, DateOnly date,
            PredictDemandHandler handler, IValidator<PredictDemandQuery> validator, CancellationToken ct) =>
        {
            var query = new PredictDemandQuery(origin, destination, date);
            await validator.ValidateAndThrowAsync(query, ct);
            return Results.Ok(await handler.HandleAsync(query, ct));
        })
        .WithName("PredictDemand")
        .WithSummary("Forecast demand for a route/date from your company's history.");

        // ── Fleet + trip management (edit/delete/lifecycle) ───────────────────────────
        group.MapPut("/buses/{id:guid}", async (
            Guid id, UpdateBusRequest body, UpdateBusHandler handler,
            IValidator<UpdateBusCommand> validator, CancellationToken ct) =>
        {
            var command = new UpdateBusCommand(id, body.SeatCount, body.Type, body.Model,
                body.SeatsPerRow ?? Bus.DefaultSeatsPerRow);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("UpdateBus").WithSummary("Edit one of your buses.");

        group.MapDelete("/buses/{id:guid}", async (Guid id, DeleteBusHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new DeleteBusCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("DeleteBus").WithSummary("Delete a bus (blocked while used by trips).");

        group.MapPut("/trips/{id:guid}", async (
            Guid id, UpdateTripRequest body, UpdateTripHandler handler,
            IValidator<UpdateTripCommand> validator, CancellationToken ct) =>
        {
            var command = new UpdateTripCommand(id, body.Origin, body.Destination,
                body.DepartureUtc, body.ArrivalUtc, body.Price, body.Currency);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("UpdateTrip").WithSummary("Edit a scheduled trip (blocked once it has bookings).");

        group.MapDelete("/trips/{id:guid}", async (Guid id, DeleteTripHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new DeleteTripCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("DeleteTrip").WithSummary("Delete a trip (blocked if it has bookings — cancel instead).");

        group.MapPost("/trips/{id:guid}/start", async (Guid id, StartTripHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new StartTripCommand(id), ct)))
        .WithName("StartTrip").WithSummary("Mark a scheduled trip as in-progress.");

        group.MapPost("/trips/{id:guid}/complete", async (Guid id, CompleteTripHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new CompleteTripCommand(id), ct)))
        .WithName("CompleteTrip").WithSummary("Mark an in-progress trip as completed.");

        group.MapPut("/trips/{id:guid}/stops", async (
            Guid id, SetTripStopsRequest body, SetTripStopsHandler handler,
            IValidator<SetTripStopsCommand> validator, CancellationToken ct) =>
        {
            var command = new SetTripStopsCommand(id, body.Stops);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("SetTripStops").WithSummary("Set the intermediate waypoints on one of your trips.");

        // ── Company profile ───────────────────────────────────────────────────────────
        group.MapGet("/company", async (GetMyCompanyHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)))
        .WithName("GetMyCompany").WithSummary("View your company profile.");

        group.MapPut("/company", async (
            UpdateCompanyProfileRequest body, UpdateMyCompanyHandler handler,
            IValidator<UpdateMyCompanyCommand> validator, CancellationToken ct) =>
        {
            var command = new UpdateMyCompanyCommand(body.Name, body.Phone);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("UpdateMyCompany").WithSummary("Edit your company profile (name, phone).");

        // ── Promo codes ───────────────────────────────────────────────────────────────
        group.MapPost("/promo-codes", async (
            CreatePromoRequest body, CreatePromoCodeHandler handler,
            IValidator<CreatePromoCodeCommand> validator, CancellationToken ct) =>
        {
            var command = new CreatePromoCodeCommand(body.Code, body.DiscountType, body.DiscountValue, body.MaxRedemptions, body.ExpiresAtUtc);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("CreatePromoCode").WithSummary("Create a promo code for your company.");

        group.MapGet("/promo-codes", async (
            int? page, int? limit, ListPromoCodesHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListPromoCodesQuery(page, limit), ct)))
        .WithName("ListPromoCodes").WithSummary("List your company's promo codes (paginated).");

        group.MapPost("/promo-codes/{id:guid}/deactivate", async (
            Guid id, DeactivatePromoCodeHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new DeactivatePromoCodeCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("DeactivatePromoCode").WithSummary("Deactivate a promo code.");

        // ── Counter / desk (manager OR staff) ────────────────────────────────────────
        var desk = app.MapGroup("/api/vendor/bookings").WithTags("Vendor · Desk")
            .RequireAuthorization(AuthorizationPolicies.VendorOrStaff)
            .RequireRateLimiting(RateLimitPolicies.Sensitive);

        desk.MapPost("/", async (
            CounterBookingRequest body, CounterBookingHandler handler,
            IValidator<CounterBookingCommand> validator, CancellationToken ct) =>
        {
            var command = new CounterBookingCommand(body.TripId, body.Passengers, body.CustomerEmail);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("CounterBooking")
        .WithSummary("Sell a ticket at the desk (cash) — immediately confirmed.");

        desk.MapGet("/", async (
            int? page, int? limit, ListCompanyBookingsHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new ListCompanyBookingsQuery(page, limit), ct)))
        .WithName("ListCompanyBookings")
        .WithSummary("List your company's bookings (paginated).");

        desk.MapPost("/{id:guid}/cancel", async (
            Guid id, CancelCompanyBookingHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new CancelCompanyBookingCommand(id), ct)))
        .WithName("CancelCompanyBooking")
        .WithSummary("Cancel + refund one of your company's bookings (cash = manual refund).");

        return app;
    }
}
