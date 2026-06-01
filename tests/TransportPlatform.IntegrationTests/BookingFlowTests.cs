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
    private static readonly int[] SeatThree = [3];
    private static readonly int[] SeatNine = [9];

    [Fact]
    public async Task Full_flow_search_hold_book_pay_confirm_ticket()
    {
        var (tripId, _, _) = await factory.SeedTripAsync();
        var (client, _) = await factory.CreateCustomerClientAsync();

        // 1. Hold a seat (owner = authenticated caller).
        var hold = await client.PostAsJsonAsync("/api/bookings/hold",
            new { tripId, seatNumbers = SeatFive });
        hold.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Create the booking (idempotency key required; passengers only).
        var create = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(new
            {
                tripId,
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

        // 4. Simulate the gateway's signed webhook reporting success (anonymous, no auth).
        await SendWebhookAsync(factory.CreateClient(), booking.Reference, succeeded: true);

        // 5. Ticket shows a confirmed booking.
        var ticket = await client.GetFromJsonAsync<TicketDto>($"/api/bookings/{booking.BookingId}/ticket", Json);
        ticket!.Status.Should().Be("Confirmed");
        ticket.QrPayload.Should().Contain(booking.Reference);
    }

    [Fact]
    public async Task Same_idempotency_key_returns_one_booking()
    {
        var (tripId, _, _) = await factory.SeedTripAsync();
        var (client, _) = await factory.CreateCustomerClientAsync();

        await client.PostAsJsonAsync("/api/bookings/hold", new { tripId, seatNumbers = SeatSeven });

        var key = Guid.NewGuid().ToString();
        async Task<BookingDto> Book()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
            {
                Content = JsonContent.Create(new
                {
                    tripId,
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

    [Fact]
    public async Task Booking_endpoints_require_authentication()
    {
        var (tripId, _, _) = await factory.SeedTripAsync();
        var anon = factory.CreateClient();

        var hold = await anon.PostAsJsonAsync("/api/bookings/hold", new { tripId, seatNumbers = SeatThree });
        hold.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_customer_cannot_read_another_customers_ticket()
    {
        var (tripId, _, _) = await factory.SeedTripAsync();
        var (alice, _) = await factory.CreateCustomerClientAsync();

        await alice.PostAsJsonAsync("/api/bookings/hold", new { tripId, seatNumbers = SeatNine });
        var create = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(new
            {
                tripId,
                passengers = new[] { new { firstName = "Alice", lastName = "Khan", seatNumber = 9 } },
            }),
        };
        create.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var booking = await (await alice.SendAsync(create)).Content.ReadFromJsonAsync<BookingDto>(Json);

        // A different customer asking for Alice's ticket gets 404 (not a 403 oracle).
        var (mallory, _) = await factory.CreateCustomerClientAsync();
        var resp = await mallory.GetAsync($"/api/bookings/{booking!.BookingId}/ticket");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task My_bookings_lists_only_my_own_bookings()
    {
        var (tripId, _, _) = await factory.SeedTripAsync();
        var (alice, _) = await factory.CreateCustomerClientAsync();

        await alice.PostAsJsonAsync("/api/bookings/hold", new { tripId, seatNumbers = SeatThree });
        var create = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(new
            {
                tripId,
                passengers = new[] { new { firstName = "Alice", lastName = "Khan", seatNumber = 3 } },
            }),
        };
        create.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var created = await (await alice.SendAsync(create)).Content.ReadFromJsonAsync<BookingDto>(Json);

        // Alice sees her booking.
        var mine = await alice.GetFromJsonAsync<List<BookingSummaryDto>>("/api/bookings", Json);
        mine.Should().ContainSingle(b => b.BookingId == created!.BookingId);

        // A different customer's list does not include it.
        var (mallory, _) = await factory.CreateCustomerClientAsync();
        var theirs = await mallory.GetFromJsonAsync<List<BookingSummaryDto>>("/api/bookings", Json);
        theirs.Should().NotContain(b => b.BookingId == created!.BookingId);
    }

    [Fact]
    public async Task Dev_simulate_endpoint_confirms_the_booking_via_the_signer_abstraction()
    {
        // Exercises POST /api/payments/dev/simulate, which signs a webhook through the
        // IPaymentWebhookSigner seam (not the concrete gateway) and runs the real confirmation.
        var (tripId, _, _) = await factory.SeedTripAsync();
        var (client, _) = await factory.CreateCustomerClientAsync();

        await client.PostAsJsonAsync("/api/bookings/hold", new { tripId, seatNumbers = SeatFive });
        var create = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(new
            {
                tripId,
                passengers = new[] { new { firstName = "Dev", lastName = "Sim", seatNumber = 5 } },
            }),
        };
        create.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var booking = await (await client.SendAsync(create)).Content.ReadFromJsonAsync<BookingDto>(Json);

        await client.PostAsJsonAsync("/api/payments/checkout", new { bookingId = booking!.BookingId });

        var simulate = await client.PostAsJsonAsync("/api/payments/dev/simulate",
            new { bookingReference = booking.Reference, succeeded = true });
        simulate.StatusCode.Should().Be(HttpStatusCode.OK);

        var ticket = await client.GetFromJsonAsync<TicketDto>($"/api/bookings/{booking.BookingId}/ticket", Json);
        ticket!.Status.Should().Be("Confirmed");
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
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "the signed webhook must confirm the booking; body: {0}", body);
    }

    private sealed record BookingDto(Guid BookingId, string Reference, decimal TotalAmount, string Currency);
    private sealed record TicketDto(Guid BookingId, string Reference, string Status, string QrPayload);
    private sealed record BookingSummaryDto(Guid BookingId, string Reference, string Status, string Origin, string Destination);
}
