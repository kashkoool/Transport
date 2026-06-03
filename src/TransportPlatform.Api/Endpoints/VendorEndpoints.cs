using System.Text;
using FluentValidation;
using TransportPlatform.Api.Security;
using TransportPlatform.Application.Common;
using TransportPlatform.Application.Fleet;
using TransportPlatform.Application.Reports;
using TransportPlatform.Application.Staff;
using TransportPlatform.Application.Trips;
using TransportPlatform.Domain.Fleet;
using TransportPlatform.Domain.Identity;

namespace TransportPlatform.Api.Endpoints;

public static class VendorEndpoints
{
    public sealed record AddBusRequest(string BusNumber, int SeatCount, BusType Type, string? Model);
    public sealed record ScheduleTripRequest(
        Guid BusId, string Origin, string Destination,
        DateTimeOffset DepartureUtc, DateTimeOffset ArrivalUtc, decimal Price, string Currency);
    public sealed record CreateStaffRequest(string Email, string Password, string FullName, StaffType StaffType);
    public sealed record AddDriverRequest(string FullName, string? Phone, string? LicenseNumber);
    public sealed record AssignDriverRequest(Guid? DriverId);

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
            var command = new AddBusCommand(body.BusNumber, body.SeatCount, body.Type, body.Model);
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
            DateTimeOffset? from, DateTimeOffset? to, VendorTripReportHandler handler, CancellationToken ct) =>
        {
            var csv = await handler.ExportCsvAsync(new VendorReportQuery(from, to), ct);
            return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", "trips-report.csv");
        })
        .WithName("VendorTripReportCsv")
        .WithSummary("Download the per-trip report as CSV.");

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

        return app;
    }
}
