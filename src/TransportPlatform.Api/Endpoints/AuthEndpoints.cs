using System.Security.Claims;
using FluentValidation;
using TransportPlatform.Api.Security;
using TransportPlatform.Application.Identity;

namespace TransportPlatform.Api.Endpoints;

public static class AuthEndpoints
{
    public sealed record RegisterRequest(string Email, string Password, string FullName);
    public sealed record LoginRequest(string Email, string Password);
    public sealed record RefreshRequest(string RefreshToken);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Brute-force-sensitive: every auth route is capped tighter than the global limit.
        var group = app.MapGroup("/api/auth").WithTags("Auth")
            .RequireRateLimiting(RateLimitPolicies.Auth);

        group.MapPost("/register", async (
            RegisterRequest body, RegisterHandler handler,
            IValidator<RegisterCommand> validator, CancellationToken ct) =>
        {
            var command = new RegisterCommand(body.Email, body.Password, body.FullName);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("Register")
        .WithSummary("Register a new customer account and receive tokens.");

        group.MapPost("/login", async (
            LoginRequest body, LoginHandler handler,
            IValidator<LoginCommand> validator, CancellationToken ct) =>
        {
            var command = new LoginCommand(body.Email, body.Password);
            await validator.ValidateAndThrowAsync(command, ct);
            return Results.Ok(await handler.HandleAsync(command, ct));
        })
        .WithName("Login")
        .WithSummary("Authenticate and receive access + refresh tokens.");

        group.MapPost("/refresh", async (
            RefreshRequest body, RefreshHandler handler, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.RefreshToken))
                return Results.BadRequest(new { code = "auth.refresh_required", message = "A refresh token is required." });
            return Results.Ok(await handler.HandleAsync(new RefreshCommand(body.RefreshToken), ct));
        })
        .WithName("Refresh")
        .WithSummary("Exchange a refresh token for a new pair (rotates; detects reuse).");

        group.MapPost("/logout", async (
            RefreshRequest body, LogoutHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(new LogoutCommand(body.RefreshToken ?? string.Empty), ct);
            return Results.NoContent();
        })
        .WithName("Logout")
        .WithSummary("Revoke a refresh token.");

        group.MapGet("/me", (ClaimsPrincipal principal) =>
            Results.Ok(new
            {
                userId = principal.FindFirstValue("sub"),
                email = principal.FindFirstValue("email"),
                roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
            }))
        .RequireAuthorization()
        .WithName("Me")
        .WithSummary("Return the authenticated caller's identity (requires Bearer token).");

        return app;
    }
}
