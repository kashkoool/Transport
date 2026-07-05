using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Domain.Payments;
using TransportPlatform.Infrastructure.Payments;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// Customer self-cancel + graduated refund by time-to-departure: ≥48h out → full refund; 24–48h →
/// half; under 24h → the seat is still freed but no money is returned. A pending booking cancels
/// with no refund.
/// </summary>
public sealed class BookingCancellationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly int[] SeatEight = [8];

    [Fact]
    public async Task Cancel_well_before_departure_is_fully_refunded_and_frees_seats()
    {
        var (tripId, _, _) = await factory.SeedTripAsync(TimeSpan.FromDays(5)); // ≥48h → full refund
        var (client, _) = await factory.CreateCustomerClientAsync();
        var booking = await ConfirmBookingAsync(client, tripId, seat: 5);

        var cancel = await client.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/cancel", new { });
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await cancel.Content.ReadFromJsonAsync<CancelResult>(Json);
        result!.Status.Should().Be("cancelled");
        result.RefundInitiated.Should().BeTrue();
        result.RefundAmount.Should().Be(booking.TotalAmount); // 100%

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.SeatAssignments.CountAsync(a => a.BookingId == booking.BookingId)).Should().Be(0);
        var refund = await db.Refunds.SingleAsync(r => r.BookingId == booking.BookingId);
        refund.Status.Should().Be(RefundStatus.Completed);
        var payment = await db.Payments.SingleAsync(p => p.BookingId == booking.BookingId);
        payment.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public async Task Cancel_between_24_and_48h_refunds_half()
    {
        var (tripId, _, _) = await factory.SeedTripAsync(TimeSpan.FromHours(36)); // 24–48h → 50%
        var (client, _) = await factory.CreateCustomerClientAsync();
        var booking = await ConfirmBookingAsync(client, tripId, seat: 6);

        var cancel = await client.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/cancel", new { });
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await cancel.Content.ReadFromJsonAsync<CancelResult>(Json);
        result!.RefundAmount.Should().Be(booking.TotalAmount / 2m); // 50%

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.SeatAssignments.CountAsync(a => a.BookingId == booking.BookingId)).Should().Be(0);
        (await db.Refunds.SingleAsync(r => r.BookingId == booking.BookingId)).Amount.Should().Be(booking.TotalAmount / 2m);
    }

    [Fact]
    public async Task Cancel_under_24h_frees_the_seat_but_refunds_nothing()
    {
        var (tripId, _, _) = await factory.SeedTripAsync(TimeSpan.FromHours(12)); // <24h → 0% but still cancellable
        var (client, _) = await factory.CreateCustomerClientAsync();
        var booking = await ConfirmBookingAsync(client, tripId, seat: 6);

        var cancel = await client.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/cancel", new { });
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await cancel.Content.ReadFromJsonAsync<CancelResult>(Json);
        result!.RefundInitiated.Should().BeFalse();
        result.RefundAmount.Should().Be(0);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.SeatAssignments.CountAsync(a => a.BookingId == booking.BookingId)).Should().Be(0); // seat freed
        (await db.Refunds.AnyAsync(r => r.BookingId == booking.BookingId)).Should().BeFalse();       // no refund
    }

    [Fact]
    public async Task Change_seat_moves_the_passenger_to_a_free_seat()
    {
        var (tripId, _, _) = await factory.SeedTripAsync(TimeSpan.FromDays(3));
        var (client, _) = await factory.CreateCustomerClientAsync();
        var booking = await ConfirmBookingAsync(client, tripId, seat: 5);

        var change = await client.PostAsJsonAsync(
            $"/api/bookings/{booking.BookingId}/change-seat", new { fromSeat = 5, toSeat = 11 });
        change.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.SeatAssignments.AnyAsync(a => a.BookingId == booking.BookingId && a.SeatNumber == 11)).Should().BeTrue();
        (await db.SeatAssignments.AnyAsync(a => a.BookingId == booking.BookingId && a.SeatNumber == 5)).Should().BeFalse();
    }

    [Fact]
    public async Task Pending_booking_is_cancelled_without_a_refund()
    {
        var (tripId, _, _) = await factory.SeedTripAsync(TimeSpan.FromDays(5));
        var (client, _) = await factory.CreateCustomerClientAsync();

        await client.PostAsJsonAsync("/api/bookings/hold", new { tripId, seatNumbers = SeatEight });
        var booking = await CreateBookingAsync(client, tripId, seat: 8); // not paid

        var cancel = await client.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/cancel", new { });
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await cancel.Content.ReadFromJsonAsync<CancelResult>(Json);
        result!.Status.Should().Be("cancelled");
        result.RefundInitiated.Should().BeFalse();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Refunds.AnyAsync(r => r.BookingId == booking.BookingId)).Should().BeFalse();
    }

    // ── helpers ───────────────────────────────────────────────────────────────────

    private async Task<BookingDto> ConfirmBookingAsync(HttpClient client, Guid tripId, int seat)
    {
        await client.PostAsJsonAsync("/api/bookings/hold", new { tripId, seatNumbers = new[] { seat } });
        var booking = await CreateBookingAsync(client, tripId, seat);
        await client.PostAsJsonAsync("/api/payments/checkout", new { bookingId = booking.BookingId });
        await SendWebhookAsync(booking.Reference, succeeded: true);
        return booking;
    }

    private static async Task<BookingDto> CreateBookingAsync(HttpClient client, Guid tripId, int seat)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(new
            {
                tripId,
                passengers = new[] { new { firstName = "Cancel", lastName = "Tester", seatNumber = seat } },
            }),
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<BookingDto>(Json))!;
    }

    private async Task SendWebhookAsync(string bookingReference, bool succeeded)
    {
        var gateway = factory.Services.GetRequiredService<SandboxPaymentGateway>();
        var payload = JsonSerializer.Serialize(new
        {
            gatewayReference = $"SBX-{Guid.NewGuid():N}",
            bookingReference,
            succeeded,
        }, Json);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-Signature", gateway.ComputeSignature(payload));
        using var resp = await factory.CreateClient().SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed record BookingDto(Guid BookingId, string Reference, decimal TotalAmount, string Currency);
    private sealed record CancelResult(string Status, bool RefundInitiated, decimal RefundAmount, string Currency);
}
