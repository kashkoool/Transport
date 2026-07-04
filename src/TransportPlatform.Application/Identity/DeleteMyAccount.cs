using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;

namespace TransportPlatform.Application.Identity;

public sealed record DeleteMyAccountResult(string Status);

/// <summary>
/// Customer self-service account deletion (GDPR erasure of the login + sessions). Blocked while the
/// customer still has an upcoming trip — those must be cancelled first (money/seat obligations).
/// Past and cancelled bookings don't block: their financial records are retained, but the account
/// and its refresh sessions are removed.
/// </summary>
public sealed class DeleteMyAccountHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IIdentityService identity, ITokenService tokens)
{
    public async Task<DeleteMyAccountResult> HandleAsync(CancellationToken ct)
    {
        var email = currentUser.RequireEmail();
        var userId = await identity.FindUserIdByEmailAsync(email, ct)
            ?? throw new NotFoundException("Account", email);

        var now = clock.UtcNow;
        var hasUpcoming = await db.Bookings.AnyAsync(b => b.CustomerEmail == email
            && (b.Status == BookingStatus.PendingPayment
                || (b.Status == BookingStatus.Confirmed && db.Trips.Any(t => t.Id == b.TripId && t.DepartureUtc > now))), ct);
        if (hasUpcoming)
            throw new ConflictException("account.has_active_bookings",
                "You have upcoming trips. Please cancel them before deleting your account.");

        // Revoke sessions first so a token can't be used in the window between delete and cascade.
        await tokens.RevokeAllForUserAsync(userId, ct);
        await identity.DeleteCustomerAsync(userId, ct);
        return new DeleteMyAccountResult("deleted");
    }
}
