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
/// Customer self-cancel + refund: a confirmed booking cancelled inside the 48h window frees its
/// seats and refunds the payment; outside the window it's blocked; a pending booking cancels with
/// no refund.
/// </summary>
public sealed class BookingCancellationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly int[] SeatEight = [8];

    [Fact]
    public async Task Confirmed_booking_cancelled_within_window_is_refunded_and_frees_seats()
    {
        var (tripId, _, _) = await factory.SeedTripAsync(TimeSpan.FromDays(5)); // well inside the window
        var (client, _) = await factory.CreateCustomerClientAsync();
        var booking = await ConfirmBookingAsync(client, tripId, seat: 5);

        var cancel = await client.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/cancel", new { });
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await cancel.Content.ReadFromJsonAsync<CancelResult>(Json);
        result!.Status.Should().Be("cancelled");
        result.RefundInitiated.Should().BeTrue();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Seats freed.
        (await db.SeatAssignments.CountAsync(a => a.BookingId == booking.BookingId)).Should().Be(0);
        // Refund recorded + completed, payment marked refunded.
        var refund = await db.Refunds.SingleAsync(r => r.BookingId == booking.BookingId);
        refund.Status.Should().Be(RefundStatus.Completed);
        var payment = await db.Payments.SingleAsync(p => p.BookingId == booking.BookingId);
        payment.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public async Task Confirmed_booking_outside_the_window_cannot_be_cancelled()
    {
        var (tripId, _, _) = await factory.SeedTripAsync(TimeSpan.FromHours(12)); // < 48h before departure
        var (client, _) = await factory.CreateCustomerClientAsync();
        var booking = await ConfirmBookingAsync(client, tripId, seat: 6);

        var cancel = await client.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/cancel", new { });
        cancel.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Still confirmed, seat still assigned, no refund.
        (await db.SeatAssignments.CountAsync(a => a.BookingId == booking.BookingId)).Should().Be(1);
        (await db.Refunds.AnyAsync(r => r.BookingId == booking.BookingId)).Should().BeFalse();
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
    private sealed record CancelResult(string Status, bool RefundInitiated);
}
