using Microsoft.EntityFrameworkCore;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Application.Common;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Payments;

namespace TransportPlatform.Application.Bookings;

public sealed record CancelCompanyBookingCommand(Guid BookingId);

public sealed record CancelCompanyBookingResult(string Status, bool RefundInitiated);

/// <summary>
/// Staff/manager cancels one of their company's bookings (desk request or trip disruption) and
/// refunds it. Unlike customer self-cancel there is no 48h window. Cash payments are flagged for a
/// manual refund (a Pending refund); gateway payments are refunded through the gateway after commit.
/// </summary>
public sealed class CancelCompanyBookingHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IPaymentGateway gateway)
{
    public async Task<CancelCompanyBookingResult> HandleAsync(CancelCompanyBookingCommand command, CancellationToken ct)
    {
        var companyId = currentUser.RequireCompanyId();
        var booking = await db.Bookings.FirstOrDefaultAsync(
            b => b.Id == command.BookingId && db.Trips.Any(t => t.Id == b.TripId && t.CompanyId == companyId), ct)
            ?? throw new NotFoundException("Booking", command.BookingId);

        switch (booking.Status)
        {
            case BookingStatus.Cancelled:
                return new CancelCompanyBookingResult("already_cancelled", false);

            case BookingStatus.PendingPayment:
                await db.ExecuteInTransactionAsync(async token =>
                {
                    var holds = await db.SeatHolds.Where(h => h.BookingId == booking.Id).ToListAsync(token);
                    db.SeatHolds.RemoveRange(holds);
                    booking.Cancel();
                    await db.SaveChangesAsync(token);
                }, ct);
                return new CancelCompanyBookingResult("cancelled", false);

            case BookingStatus.Confirmed:
                break;

            default:
                throw new ConflictException("booking.not_cancellable", "This booking can no longer be cancelled.");
        }

        Guid? refundId = null;
        var isCash = false;
        await db.ExecuteInTransactionAsync(async token =>
        {
            var assignments = await db.SeatAssignments.Where(a => a.BookingId == booking.Id).ToListAsync(token);
            db.SeatAssignments.RemoveRange(assignments); // free the seats

            var payment = await db.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.Id, token);
            if (payment is { Status: PaymentStatus.Completed })
            {
                isCash = string.Equals(payment.Gateway, "Cash", StringComparison.OrdinalIgnoreCase);
                var refund = new Refund(payment.Id, booking.Id, new Money(payment.Amount, payment.Currency),
                    "Operator cancellation", $"refund-{booking.Id:N}");
                db.Refunds.Add(refund);
                refundId = refund.Id;
            }

            booking.CancelConfirmed();
            await db.SaveChangesAsync(token);
        }, ct);

        // Cash is refunded manually at the desk → leave it Pending. Gateway payments are refunded
        // through the gateway after the transaction commits.
        var refundInitiated = false;
        if (refundId is { } id && !isCash)
            refundInitiated = await RefundProcessing.TryProcessAsync(db, gateway, id, ct);

        return new CancelCompanyBookingResult("cancelled", refundInitiated);
    }
}
