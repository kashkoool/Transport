using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.Application.Payments;

public sealed record StartCheckoutCommand(Guid BookingId);

public sealed record StartCheckoutResult(string CheckoutUrl, string GatewayReference);

/// <summary>
/// Creates (or re-uses) the payment record for a pending booking and asks the external
/// gateway for a hosted-checkout URL. No card data is ever handled here. The booking is
/// scoped to the authenticated owner so a caller can only pay for their own booking.
/// </summary>
public sealed class StartCheckoutHandler(IApplicationDbContext db, IPaymentGateway gateway, ICurrentUser currentUser)
{
    public async Task<StartCheckoutResult> HandleAsync(StartCheckoutCommand command, CancellationToken ct)
    {
        var email = currentUser.RequireEmail();

        var booking = await db.Bookings
            .FirstOrDefaultAsync(b => b.Id == command.BookingId && b.CustomerEmail == email, ct)
            ?? throw new NotFoundException("Booking", command.BookingId);

        if (booking.Status == BookingStatus.Confirmed)
            throw new ConflictException("booking.already_paid", "This booking is already paid.");
        if (booking.Status != BookingStatus.PendingPayment)
            throw new ConflictException("booking.not_payable", "This booking can no longer be paid.");

        // One payment per booking; the idempotency key is the booking's own key.
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.Id, ct);
        if (payment is null)
        {
            payment = new Payment(booking.Id, gateway.Name, booking.Total, booking.IdempotencyKey);
            db.Payments.Add(payment);
            await db.SaveChangesAsync(ct);
        }

        var session = await gateway.CreateCheckoutAsync(new CheckoutRequest(
            booking.Id, booking.Reference, booking.TotalAmount, booking.Currency,
            booking.CustomerEmail, booking.IdempotencyKey), ct);

        payment.AttachGatewayReference(session.GatewayReference);
        await db.SaveChangesAsync(ct);

        return new StartCheckoutResult(session.CheckoutUrl, session.GatewayReference);
    }
}
