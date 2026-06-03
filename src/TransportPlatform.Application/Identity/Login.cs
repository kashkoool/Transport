using FluentValidation;
using Microsoft.Extensions.Options;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Identity;

public sealed record LoginCommand(string Email, string Password);

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

/// <summary>Authenticates a user and issues a token pair. Generic error to avoid user enumeration.</summary>
public sealed class LoginHandler(IIdentityService identity, ITokenService tokens, IOptions<AuthOptions> authOptions)
{
    private readonly AuthOptions _auth = authOptions.Value;

    public async Task<AuthResult> HandleAsync(LoginCommand command, CancellationToken ct)
    {
        var user = await identity.ValidateCredentialsAsync(command.Email, command.Password, ct)
            ?? throw new UnauthorizedException("auth.invalid_credentials", "Invalid email or password.");

        // Only after the password is verified do we reveal verification state — the credentials are
        // correct, so this leaks nothing to an attacker but lets a real user know to verify (and the
        // SPA can offer "resend"). Gated by config so dev/test and pre-verification setups still work.
        if (_auth.RequireEmailVerification && !user.EmailConfirmed)
            throw new UnauthorizedException("auth.email_not_verified",
                "Please verify your email address before signing in.");

        var issued = await tokens.IssueAsync(user.UserId, user.Email, user.Roles, user.CompanyId, ct);
        return AuthResult.From(issued, user);
    }
}
