using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Domain.Fleet;
using TransportPlatform.Domain.Trips;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// Ratings & reviews: a customer can review a confirmed booking on a departed trip, it appears in
/// the public trip reviews (display name only — no email leak), and a booking can be reviewed once.
/// </summary>
public sealed class ReviewTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Customer_reviews_a_travelled_trip_and_it_shows_publicly_without_pii()
    {
        var (client, email) = await factory.CreateCustomerClientAsync();
        var (tripId, bookingId) = await SeedDepartedConfirmedBookingAsync(email);

        var create = await client.PostAsJsonAsync("/api/reviews", new { bookingId, rating = 5, comment = "Smooth ride" });
        create.StatusCode.Should().Be(HttpStatusCode.OK);

        // Public view (no auth): average + review with a display name, and NO email anywhere.
        var anon = factory.CreateClient();
        var resp = await anon.GetAsync($"/api/trips/{tripId}/reviews");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().NotContain(email, "the customer email must never be exposed publicly");

        var summary = JsonSerializer.Deserialize<ReviewSummaryDto>(body, Json);
        summary!.AverageRating.Should().Be(5);
        summary.Count.Should().Be(1);
        summary.Reviews.Should().ContainSingle(r => r.Rating == 5 && !string.IsNullOrWhiteSpace(r.DisplayName));

        // One review per booking.
        var again = await client.PostAsJsonAsync("/api/reviews", new { bookingId, rating = 3, comment = "again" });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<(Guid TripId, Guid BookingId)> SeedDepartedConfirmedBookingAsync(string customerEmail)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var company = new Company("Review Lines", $"v-{Guid.NewGuid():N}@example.com", null);
        company.Activate();
        var bus = new Bus(company.Id, $"RV-{Guid.NewGuid():N}"[..10], 40, BusType.Standard);
        var departed = DateTimeOffset.UtcNow.AddDays(-1);
        var trip = new Trip(company.Id, bus.Id, "Damascus", "Homs", departed, departed.AddHours(2), 40, new Money(50_000m, "SYP"));

        var passengers = new[] { new Passenger("Sara", "Khan", 4) };
        var booking = Booking.Create(trip.Id, customerEmail, $"BK-REV{Guid.NewGuid():N}"[..12],
            passengers, trip.Fare, $"rev-{Guid.NewGuid():N}");
        booking.Confirm();

        db.Companies.Add(company);
        db.Buses.Add(bus);
        db.Trips.Add(trip);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        return (trip.Id, booking.Id);
    }

    private sealed record ReviewDto(Guid Id, int Rating, string? Comment, string DisplayName, DateTimeOffset CreatedAtUtc);
    private sealed record ReviewSummaryDto(double AverageRating, int Count, IReadOnlyList<ReviewDto> Reviews);
}
