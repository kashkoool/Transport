using FluentValidation;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;

namespace TransportPlatform.Application.Identity;

/// <summary>Returns the authenticated caller's own profile.</summary>
public sealed class GetProfileHandler(IIdentityService identity, ICurrentUser currentUser)
{
    public async Task<UserProfile> HandleAsync(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        return await identity.GetProfileAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);
    }
}

public sealed record UpdateProfileCommand(string FullName, string? Phone);

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        // E.164-ish: optional leading +, digits/spaces/hyphens, bounded length.
        RuleFor(x => x.Phone).MaximumLength(30).Matches(@"^\+?[0-9\s\-]{7,30}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Enter a valid phone number.");
    }
}

/// <summary>Updates the authenticated caller's own profile (name + optional phone). Email is read-only.</summary>
public sealed class UpdateProfileHandler(IIdentityService identity, ICurrentUser currentUser)
{
    public async Task<UserProfile> HandleAsync(UpdateProfileCommand command, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        return await identity.UpdateProfileAsync(userId, command.FullName, command.Phone, ct)
            ?? throw new NotFoundException("User", userId);
    }
}
