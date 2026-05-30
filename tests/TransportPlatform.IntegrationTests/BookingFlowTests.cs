using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Infrastructure.Payments;

namespace TransportPlatform.IntegrationTests;

public sealed class BookingFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly int[] SeatFive = [5];
    private static readonly int[] SeatSeven = [7];

    [Fact]
    public async Task Full_flow_search_hold_book_pay_confirm_ticket()
    {
        var (tripId, _, _) = await factory.SeedTripAsync();
        var client = factory.CreateClient();

        // 1. Hold a seat.
        var hold = await client.PostAsJsonAsync("/api/bookings/hold",
            new { tripId, seatNumbers = SeatFive, heldBy = "alice@example.com" });
        hold.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Create the booking (idempotency key required).
        var create = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(new
            {
                tripId,
                customerEmail = "alice@example.com",
                heldBy = "alice@example.com",
                passengers = new[] { new { firstName = "Alice", lastName = "Khan", seatNumber = 5 } },
            }),
        };
        create.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var createResp = await client.SendAsync(create);
        createResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var booking = await createResp.Content.ReadFromJsonAsync<BookingDto>(Json);
        booking!.Reference.Should().StartWith("BK-");

        // 3. Start checkout (external gateway — returns a hosted URL, no card data).
        var checkout = await client.PostAsJsonAsync("/api/payments/checkout", new { bookingId = booking.BookingId });
        checkout.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Simulate the gateway's signed webhook reporting success.
        await SendWebhookAsync(client, booking.Reference, succeeded: true);

        // 5. Ticket shows a confirmed booking.
        var ticket = await client.GetFromJsonAsync<TicketDto>($"/api/bookings/{booking.BookingId}/ticket", Json);
        ticket!.Status.Should().Be("Confirmed");
        ticket.QrPayload.Should().Contain(booking.Reference);
    }

    [Fact]
    public async Task Same_idempotency_key_returns_one_booking()
    {
        var (tripId, _, _) = await factory.SeedTripAsync();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/bookings/hold",
            new { tripId, seatNumbers = SeatSeven, heldBy = "bob@example.com" });

        var key = Guid.NewGuid().ToString();
        async Task<BookingDto> Book()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
            {
                Content = JsonContent.Create(new
                {
                    tripId,
                    customerEmail = "bob@example.com",
                    heldBy = "bob@example.com",
                    passengers = new[] { new { firstName = "Bob", lastName = "Lee", seatNumber = 7 } },
                }),
            };
            req.Headers.Add("Idempotency-Key", key);
            var resp = await client.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<BookingDto>(Json))!;
        }

        var first = await Book();
        var second = await Book();

        second.BookingId.Should().Be(first.BookingId); // same key → same booking, no duplicate
    }

    private async Task SendWebhookAsync(HttpClient client, string bookingReference, bool succeeded)
    {
        var gateway = factory.Services.GetRequiredService<SandboxPaymentGateway>();
        var payload = JsonSerializer.Serialize(new
        {
            gatewayReference = $"SBX-{Guid.NewGuid():N}",
            bookingReference,
            succeeded,
        }, Json);

        var signature = gateway.ComputeSignature(payload);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-Signature", signature);
        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    private sealed record BookingDto(Guid BookingId, string Reference, decimal TotalAmount, string Currency);
    private sealed record TicketDto(Guid BookingId, string Reference, string Status, string QrPayload);
}
