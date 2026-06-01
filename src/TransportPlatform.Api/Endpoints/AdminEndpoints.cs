using FluentValidation;
using TransportPlatform.Api.Security;
using TransportPlatform.Application.Common;
using TransportPlatform.Application.Companies;
using TransportPlatform.Domain.Companies;

namespace TransportPlatform.Api.Endpoints;

public static class AdminEndpoints
{
    public sealed record CreateCompanyRequest(string Name, string Email, string? Phone);
    public sealed record CreateManagerRequest(string Email, string Password, string FullName);

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // Platform admins only. Sensitive tier because these are high-value mutations.
        var group = app.MapGroup("/api/admin/companies").WithTags("Admin · Companies")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly)
            .RequireRateLimiting(RateLimitPolicies.Sensitive);

        group.MapPost("/", async (
            CreateCompanyRequest body, CreateCompanyHandler handler,
            IValidator<CreateCompanyCommand> validator, CancellationToken ct) =>
        {
            var command = new CreateCompanyCommand(body.Name, body.Email, body.Phone);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("CreateCompany")
        .WithSummary("Onboard a new vendor company (starts Pending).");

        group.MapGet("/", async (
            int? page, int? limit, CompanyStatus? status,
            ListCompaniesHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new ListCompaniesQuery(page, limit, status), ct);
            return Results.Ok(result);
        })
        .WithName("ListCompanies")
        .WithSummary("List vendor companies (paginated, optional status filter).");

        group.MapPost("/{id:guid}/activate", async (
            Guid id, SetCompanyStatusHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new SetCompanyStatusCommand(id, Activate: true), ct)))
        .WithName("ActivateCompany")
        .WithSummary("Activate a company so its trips can be sold.");

        group.MapPost("/{id:guid}/suspend", async (
            Guid id, SetCompanyStatusHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new SetCompanyStatusCommand(id, Activate: false), ct)))
        .WithName("SuspendCompany")
        .WithSummary("Suspend a company.");

        group.MapPost("/{id:guid}/manager", async (
            Guid id, CreateManagerRequest body, CreateCompanyManagerHandler handler,
            IValidator<CreateCompanyManagerCommand> validator, CancellationToken ct) =>
        {
            var command = new CreateCompanyManagerCommand(id, body.Email, body.Password, body.FullName);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("CreateCompanyManager")
        .WithSummary("Create the vendor-manager login bound to a company.");

        return app;
    }
}
