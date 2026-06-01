using FluentValidation;
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
public sealed class LoginHandler(IIdentityService identity, ITokenService tokens)
{
    public async Task<AuthResult> HandleAsync(LoginCommand command, CancellationToken ct)
    {
        var user = await identity.ValidateCredentialsAsync(command.Email, command.Password, ct)
            ?? throw new UnauthorizedException("auth.invalid_credentials", "Invalid email or password.");
        var issued = await tokens.IssueAsync(user.UserId, user.Email, user.Roles, user.CompanyId, ct);
        return AuthResult.From(issued, user);
    }
}
