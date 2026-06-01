using System.Security.Claims;
using FluentValidation;
using TransportPlatform.Api.Security;
using TransportPlatform.Application.Identity;

namespace TransportPlatform.Api.Endpoints;

public static class AuthEndpoints
{
    public sealed record RegisterRequest(string Email, string Password, string FullName);
    public sealed record LoginRequest(string Email, string Password);
    // Optional: a browser SPA carries the refresh token in the HttpOnly `rt` cookie and sends
    // no body; API/native clients may still pass it in the body. Either source is accepted.
    public sealed record RefreshRequest(string? RefreshToken);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Brute-force-sensitive: every auth route is capped tighter than the global limit.
        var group = app.MapGroup("/api/auth").WithTags("Auth")
            .RequireRateLimiting(RateLimitPolicies.Auth);

        group.MapPost("/register", async (
            RegisterRequest body, RegisterHandler handler, HttpResponse response,
            IValidator<RegisterCommand> validator, IHostEnvironment env, CancellationToken ct) =>
        {
            var command = new RegisterCommand(body.Email, body.Password, body.FullName);
            await validator.ValidateAndThrowAsync(command, ct);
            var result = await handler.HandleAsync(command, ct);
            RefreshCookie.Set(response, result.RefreshToken, result.RefreshTokenExpiresAtUtc, RefreshCookie.SecureFor(env));
            return Results.Ok(result);
        })
        .WithName("Register")
        .WithSummary("Register a new customer account and receive tokens.");

        group.MapPost("/login", async (
            LoginRequest body, LoginHandler handler, HttpResponse response,
            IValidator<LoginCommand> validator, IHostEnvironment env, CancellationToken ct) =>
        {
            var command = new LoginCommand(body.Email, body.Password);
            await validator.ValidateAndThrowAsync(command, ct);
            var result = await handler.HandleAsync(command, ct);
            RefreshCookie.Set(response, result.RefreshToken, result.RefreshTokenExpiresAtUtc, RefreshCookie.SecureFor(env));
            return Results.Ok(result);
        })
        .WithName("Login")
        .WithSummary("Authenticate and receive access + refresh tokens.");

        group.MapPost("/refresh", async (
            HttpContext http, RefreshHandler handler, IHostEnvironment env, CancellationToken ct,
            RefreshRequest? body = null) =>
        {
            // Body first (back-compat for API/native clients and existing tests), then the
            // HttpOnly cookie used by the SPA.
            var token = body?.RefreshToken is { Length: > 0 } b ? b : RefreshCookie.Read(http.Request);
            if (string.IsNullOrWhiteSpace(token))
                return Results.BadRequest(new { code = "auth.refresh_required", message = "A refresh token is required." });

            var rotated = await handler.HandleAsync(new RefreshCommand(token), ct);
            // Rotation: replace the cookie with the new refresh token so the SPA stays logged in.
            RefreshCookie.Set(http.Response, rotated.RefreshToken, rotated.RefreshTokenExpiresAtUtc, RefreshCookie.SecureFor(env));
            return Results.Ok(rotated);
        })
        .WithName("Refresh")
        .WithSummary("Exchange a refresh token for a new pair (rotates; detects reuse).");

        group.MapPost("/logout", async (
            HttpContext http, LogoutHandler handler, IHostEnvironment env, CancellationToken ct,
            RefreshRequest? body = null) =>
        {
            var token = body?.RefreshToken is { Length: > 0 } b ? b : RefreshCookie.Read(http.Request);
            await handler.HandleAsync(new LogoutCommand(token ?? string.Empty), ct);
            // Always clear the cookie, even if there was no token to revoke (idempotent logout).
            RefreshCookie.Delete(http.Response, RefreshCookie.SecureFor(env));
            return Results.NoContent();
        })
        .WithName("Logout")
        .WithSummary("Revoke a refresh token and clear the refresh cookie.");

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
