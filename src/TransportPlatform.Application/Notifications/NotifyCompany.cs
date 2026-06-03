using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Notifications;

namespace TransportPlatform.Application.Notifications;

public sealed record NotifyCompanyCommand(Guid CompanyId, string Title, string Message, string Type);

public sealed class NotifyCompanyValidator : AbstractValidator<NotifyCompanyCommand>
{
    public NotifyCompanyValidator()
    {
        RuleFor(x => x.CompanyId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Type).MaximumLength(20);
    }
}

public sealed record NotifyCompanyResult(int Recipients);

/// <summary>
/// Admin → company message: creates an in-app notification for each of the company's managers and
/// pushes it live. Returns how many recipients were notified.
/// </summary>
public sealed class NotifyCompanyHandler(
    IApplicationDbContext db, IIdentityService identity, IRealtimeNotifier realtime)
{
    public async Task<NotifyCompanyResult> HandleAsync(NotifyCompanyCommand command, CancellationToken ct)
    {
        if (!await db.Companies.AnyAsync(c => c.Id == command.CompanyId, ct))
            throw new NotFoundException("Company", command.CompanyId);

        var managerIds = await identity.ListCompanyManagerIdsAsync(command.CompanyId, ct);
        var type = string.IsNullOrWhiteSpace(command.Type) ? "info" : command.Type;

        var created = new List<Notification>(managerIds.Count);
        foreach (var managerId in managerIds)
        {
            var notification = new Notification(managerId, command.Title, command.Message, type);
            db.Notifications.Add(notification);
            created.Add(notification);
        }
        await db.SaveChangesAsync(ct);

        foreach (var notification in created)
            await realtime.NotifyUserAsync(notification.RecipientUserId, new
            {
                id = notification.Id,
                title = notification.Title,
                message = notification.Message,
                type = notification.Type,
                isRead = notification.IsRead,
                createdAtUtc = notification.CreatedAtUtc,
            }, ct);

        return new NotifyCompanyResult(created.Count);
    }
}
