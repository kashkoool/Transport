using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;

namespace TransportPlatform.Application.Admin;

public sealed record ListCustomersQuery(int? Page, int? Limit, string? Search = null);

/// <summary>Admin: list customer accounts (paged, optional name/email search).</summary>
public sealed class ListCustomersHandler(IIdentityService identity)
{
    public async Task<PagedResult<CustomerAccount>> HandleAsync(ListCustomersQuery query, CancellationToken ct)
    {
        var page = new PageRequest(query.Page, query.Limit);
        var total = await identity.CountCustomersAsync(query.Search, ct);
        var items = await identity.ListCustomersAsync(page.Skip, page.Limit, query.Search, ct);
        return new PagedResult<CustomerAccount>(items, total, page.Page, page.Limit);
    }
}

public sealed record SetCustomerSuspendedCommand(Guid UserId, bool Suspended);

/// <summary>Admin: suspend or reactivate a customer account (blocks/restores their login).</summary>
public sealed class SetCustomerSuspendedHandler(IIdentityService identity)
{
    public async Task HandleAsync(SetCustomerSuspendedCommand command, CancellationToken ct)
    {
        if (!await identity.SetCustomerSuspendedAsync(command.UserId, command.Suspended, ct))
            throw new NotFoundException("Customer", command.UserId);
    }
}

public sealed record DeleteCustomerCommand(Guid UserId);

/// <summary>
/// Admin: delete a customer account — blocked while they have non-cancelled bookings (financial
/// history must be preserved; cancel/settle those first). Guarded + 404 for non-customers.
/// </summary>
public sealed class DeleteCustomerHandler(IIdentityService identity, IApplicationDbContext db)
{
    public async Task HandleAsync(DeleteCustomerCommand command, CancellationToken ct)
    {
        var customer = await identity.FindCustomerAsync(command.UserId, ct)
            ?? throw new NotFoundException("Customer", command.UserId);

        if (await db.Bookings.AnyAsync(b => b.CustomerEmail == customer.Email && b.Status != BookingStatus.Cancelled, ct))
            throw new ConflictException("customer.has_bookings",
                "This customer has active bookings and can't be deleted.");

        if (!await identity.DeleteCustomerAsync(command.UserId, ct))
            throw new NotFoundException("Customer", command.UserId);
    }
}
