using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// The headline guarantee: a seat can never be sold twice, even under a race. This is the
/// exact failure the old MongoDB system fought (see ../../grad-project TRANSACTION_FIX.md).
/// </summary>
public sealed class ConcurrencyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Parallel_holds_for_the_same_seat_let_exactly_one_win()
    {
        var (tripId, _, _) = await factory.SeedTripAsync();
        const int seat = 12;
        const int contenders = 20;

        // Fire many simultaneous hold requests for the SAME seat.
        var tasks = Enumerable.Range(0, contenders).Select(async i =>
        {
            var client = factory.CreateClient();
            var resp = await client.PostAsJsonAsync("/api/bookings/hold",
                new { tripId, seatNumbers = new[] { seat }, heldBy = $"user{i}@example.com" });
            return resp.StatusCode;
        });

        var results = await Task.WhenAll(tasks);

        var ok = results.Count(s => s == HttpStatusCode.OK);
        var conflict = results.Count(s => s == HttpStatusCode.Conflict);

        ok.Should().Be(1, "exactly one request may hold a given seat");
        conflict.Should().Be(contenders - 1, "every other contender must be rejected with 409");

        // And the database must contain exactly one active hold for that seat.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var activeHolds = db.SeatHolds.Count(h => h.TripId == tripId && h.SeatNumber == seat && !h.Consumed);
        activeHolds.Should().Be(1);
    }
}
