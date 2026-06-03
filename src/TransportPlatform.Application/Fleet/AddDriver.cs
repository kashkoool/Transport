using FluentValidation;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Fleet;

namespace TransportPlatform.Application.Fleet;

public sealed record AddDriverCommand(string FullName, string? Phone, string? LicenseNumber);

public sealed class AddDriverValidator : AbstractValidator<AddDriverCommand>
{
    public AddDriverValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.LicenseNumber).MaximumLength(60);
    }
}

/// <summary>A vendor manager adds a driver to their OWN company (company id from the caller).</summary>
public sealed class AddDriverHandler(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<DriverDto> HandleAsync(AddDriverCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var driver = new Driver(companyId, command.FullName, command.Phone, command.LicenseNumber);
        db.Drivers.Add(driver);
        await db.SaveChangesAsync(ct);
        return DriverDto.From(driver);
    }
}
