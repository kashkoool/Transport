using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TransportPlatform.Api.Security;
using TransportPlatform.Application.Common;
using TransportPlatform.Application.Identity;
using TransportPlatform.Infrastructure.Identity;

namespace TransportPlatform.Api.Endpoints;

public static class AuthEndpoints
{
    public sealed record RegisterRequest(string Email, string Password, string FullName);
    public sealed record LoginRequest(string Email, string Password);
    // Optional: a browser SPA carries the refresh token in the HttpOnly `rt` cookie and sends
    // no body; API/native clients may still pass it in the body. Either source is accepted.
    public sealed record RefreshRequest(string? RefreshToken);
    public sealed record ForgotPasswordRequest(string Email);
    public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
    public sealed record VerifyEmailRequest(string Email, string Token);
    public sealed record ResendVerificationRequest(string Email);

    private static readonly object GenericEmailAck =
        new { message = "If an account exists for that email, a message has been sent." };

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

        group.MapPost("/forgot-password", async (
            ForgotPasswordRequest body, RequestPasswordResetHandler handler, CancellationToken ct) =>
        {
            // Always the same generic response — never reveal whether the account exists.
            if (!string.IsNullOrWhiteSpace(body.Email))
                await handler.HandleAsync(new RequestPasswordResetCommand(body.Email), ct);
            return Results.Ok(GenericEmailAck);
        })
        .WithName("ForgotPassword")
        .WithSummary("Send a password-reset link if the email maps to an account (always 200).");

        group.MapPost("/reset-password", async (
            ResetPasswordRequest body, ResetPasswordHandler handler,
            IValidator<ResetPasswordCommand> validator, CancellationToken ct) =>
        {
            var command = new ResetPasswordCommand(body.Email, body.Token, body.NewPassword);
            await validator.ValidateAndThrowAsync(command, ct);
            var ok = await handler.HandleAsync(command, ct);
            return ok
                ? Results.Ok(new { message = "Your password has been reset." })
                : Results.BadRequest(new { code = "auth.reset_invalid", message = "This reset link is invalid or has expired." });
        })
        .WithName("ResetPassword")
        .WithSummary("Reset a password using an emailed token; revokes existing sessions.");

        group.MapPost("/verify-email", async (
            VerifyEmailRequest body, VerifyEmailHandler handler, CancellationToken ct) =>
        {
            var ok = await handler.HandleAsync(new VerifyEmailCommand(body.Email, body.Token), ct);
            return ok
                ? Results.Ok(new { message = "Your email is verified." })
                : Results.BadRequest(new { code = "auth.verify_invalid", message = "This verification link is invalid or has expired." });
        })
        .WithName("VerifyEmail")
        .WithSummary("Confirm an email address using an emailed token.");

        group.MapPost("/resend-verification", async (
            ResendVerificationRequest body, ResendVerificationHandler handler, CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(body.Email))
                await handler.HandleAsync(new ResendVerificationCommand(body.Email), ct);
            return Results.Ok(GenericEmailAck);
        })
        .WithName("ResendVerification")
        .WithSummary("Re-send the verification email if applicable (always 200).");

        // ── Google OAuth (sign-in + sign-up) ────────────────────────────────────────
        // Backend Authorization-Code redirect flow: the client secret stays server-side and the
        // result reuses the same refresh-cookie path as password login. Endpoints 404 when Google
        // isn't configured (dev/CI without credentials).
        group.MapGet("/google/start", (string? returnUrl, IOptions<GoogleAuthOptions> google) =>
        {
            if (!google.Value.Enabled)
                return Results.NotFound(new { code = "auth.google_disabled", message = "Google sign-in is not configured." });

            var redirect = $"/api/auth/google/callback?returnUrl={Uri.EscapeDataString(SafeReturnPath(returnUrl))}";
            return Results.Challenge(new AuthenticationProperties { RedirectUri = redirect }, [GoogleAuthOptions.Scheme]);
        })
        .WithName("GoogleStart")
        .WithSummary("Begin Google OAuth sign-in/sign-up (redirects to Google).");

        group.MapGet("/google/callback", async (
            HttpContext http, GoogleSignInHandler handler, IOptions<GoogleAuthOptions> google,
            IConfiguration config, IHostEnvironment env, string? returnUrl, CancellationToken ct) =>
        {
            if (!google.Value.Enabled)
                return Results.NotFound();

            var frontend = (config["Email:FrontendBaseUrl"] ?? "http://localhost:8080").TrimEnd('/');

            // Read the external identity Google parked in the short-lived cookie, then clear it.
            var auth = await http.AuthenticateAsync(GoogleAuthOptions.ExternalCookieScheme);
            await http.SignOutAsync(GoogleAuthOptions.ExternalCookieScheme);

            if (!auth.Succeeded || auth.Principal is null)
                return Results.Redirect($"{frontend}/login?error=google");

            var principal = auth.Principal;
            var email = principal.FindFirstValue(ClaimTypes.Email);
            var providerKey = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var fullName = principal.FindFirstValue(ClaimTypes.Name);
            var emailVerified = string.Equals(
                principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(providerKey))
                return Results.Redirect($"{frontend}/login?error=google");

            try
            {
                var result = await handler.HandleAsync(
                    new GoogleSignInCommand(providerKey, email, fullName, emailVerified), ct);
                // Mint our own session: refresh token in the HttpOnly cookie, then bounce to the SPA,
                // which exchanges the cookie for an in-memory access token via restoreSession().
                RefreshCookie.Set(http.Response, result.RefreshToken, result.RefreshTokenExpiresAtUtc, RefreshCookie.SecureFor(env));
                return Results.Redirect($"{frontend}/auth/callback?returnUrl={Uri.EscapeDataString(SafeReturnPath(returnUrl))}");
            }
            catch (ConflictException)
            {
                // e.g. the email maps to an unverified local account — ask them to sign in normally first.
                return Results.Redirect($"{frontend}/login?error=google_link");
            }
        })
        .WithName("GoogleCallback")
        .WithSummary("Google OAuth callback: signs the user in and redirects to the web app.");

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

    /// <summary>
    /// Only allow app-relative return paths ("/path") so the OAuth round-trip can never be turned
    /// into an open redirect to an attacker-controlled site.
    /// </summary>
    private static string SafeReturnPath(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            ? returnUrl
            : "/";
}
