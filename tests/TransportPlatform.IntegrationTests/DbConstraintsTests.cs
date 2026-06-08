using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Payments;
using TransportPlatform.Domain.Reviews;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// The schema's defence-in-depth guarantees, asserted against the real Postgres: CHECK constraints
/// reject impossible values even when the domain guards are bypassed, and the financial-record FKs
/// are Restrict so deleting a booking can never silently erase its payment history.
/// </summary>
public sealed class DbConstraintsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Deleting_a_booking_that_has_a_payment_is_blocked()
    {
        var bookingId = await SeedPendingBookingAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Payments.Add(new Payment(bookingId, "Test", new Money(100m, "SYP"), Guid.NewGuid().ToString()));
            await db.SaveChangesAsync();
        }

        // A FRESH context that doesn't track the payment, so the block comes from the DB's Restrict
        // FK (not EF's client-side severing): the booking — financial history — can't be hard-deleted.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var booking = await db.Bookings.FirstAsync(b => b.Id == bookingId);
            db.Bookings.Remove(booking);
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task A_negative_payment_amount_is_rejected_by_the_DB()
    {
        var bookingId = await SeedPendingBookingAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payment = new Payment(bookingId, "Test", new Money(100m, "SYP"), Guid.NewGuid().ToString());
        ForcePrivate(payment, nameof(Payment.Amount), -1m); // bypass the domain guard
        db.Payments.Add(payment);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_rating_outside_one_to_five_is_rejected_by_the_DB()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Reviews have no FKs (soft references), so this isolates the CK_review_rating_range check.
        var review = new Review(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "rider@example.com", "Test R.", rating: 5, comment: null);
        ForcePrivate(review, nameof(Review.Rating), 6); // bypass the domain guard
        db.Set<Review>().Add(review);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    // ── helpers ───────────────────────────────────────────────────────────────────

    /// <summary>Seed a pending booking (one passenger, no payment yet) and return its id.</summary>
    private async Task<Guid> SeedPendingBookingAsync()
    {
        var (tripId, _, price) = await factory.SeedTripAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var booking = Booking.Create(
            tripId, "rider@example.com", $"REF-{Guid.NewGuid():N}"[..12],
            [new Passenger("Test", "Rider", 1)], new Money(price, "SYP"), Guid.NewGuid().ToString());
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking.Id;
    }

    /// <summary>Set a property through its non-public setter, to drive a value the domain forbids.</summary>
    private static void ForcePrivate(object target, string property, object value) =>
        target.GetType().GetProperty(property)!.GetSetMethod(nonPublic: true)!.Invoke(target, [value]);
}
