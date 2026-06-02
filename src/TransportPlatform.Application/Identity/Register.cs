using FluentValidation;
using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Application.Identity;

public sealed record RegisterCommand(string Email, string Password, string FullName);

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        // Password strength is also enforced by Identity; this is the fast boundary check.
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
    }
}

/// <summary>Registers a new customer and immediately issues their first token pair.</summary>
public sealed class RegisterHandler(IIdentityService identity, ITokenService tokens, IAuthEmailService authEmail)
{
    public async Task<AuthResult> HandleAsync(RegisterCommand command, CancellationToken ct)
    {
        var user = await identity.RegisterCustomerAsync(command.Email, command.Password, command.FullName, ct);

        // Send a verification email — entirely best-effort. The whole block is guarded (not just
        // the send) so a token-provider hiccup can't roll back an otherwise-successful sign-up;
        // the sender itself also logs/swallows transport failures. Verification isn't enforced for
        // login yet, so resend covers anyone who slips through.
        try
        {
            var verifyToken = await identity.GenerateEmailConfirmationTokenAsync(user.UserId, ct);
            if (!string.IsNullOrEmpty(verifyToken))
                await authEmail.SendEmailVerificationAsync(user.Email, verifyToken, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Swallow: the account exists and is usable; verification can be re-requested.
        }

        var issued = await tokens.IssueAsync(user.UserId, user.Email, user.Roles, user.CompanyId, ct);
        return AuthResult.From(issued, user);
    }
}
