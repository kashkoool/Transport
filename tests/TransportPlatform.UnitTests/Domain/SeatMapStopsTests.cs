using FluentAssertions;
using TransportPlatform.Domain.Bookings;
using TransportPlatform.Domain.Common;
using TransportPlatform.Domain.Fleet;
using TransportPlatform.Domain.Trips;

namespace TransportPlatform.UnitTests.Domain;

/// <summary>Layout, document and waypoint invariants for the seat-map / stops feature.</summary>
public class SeatMapStopsTests
{
    private static Trip NewTrip() => new(
        Guid.NewGuid(), Guid.NewGuid(), "Damascus", "Aleppo",
        DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(4),
        40, new Money(50_000m, "SYP"));

    [Fact]
    public void Bus_defaults_to_four_seats_per_row()
    {
        var bus = new Bus(Guid.NewGuid(), "BUS-1", 40, BusType.Standard);
        bus.SeatsPerRow.Should().Be(Bus.DefaultSeatsPerRow);
    }

    [Fact]
    public void Bus_rejects_an_out_of_range_layout()
    {
        var act = () => new Bus(Guid.NewGuid(), "BUS-1", 40, BusType.Standard, seatsPerRow: Bus.MaxSeatsPerRow + 1);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("bus.layout_invalid");
    }

    [Fact]
    public void Passenger_keeps_a_trimmed_document_and_nulls_blanks()
    {
        var p = new Passenger("Ann", "Lee", 3, "  Passport ", "  X99 ");
        p.DocumentType.Should().Be("Passport");
        p.DocumentNumber.Should().Be("X99");

        var noDoc = new Passenger("Ann", "Lee", 3, "   ", null);
        noDoc.DocumentType.Should().BeNull();
        noDoc.DocumentNumber.Should().BeNull();
    }

    [Fact]
    public void Passenger_rejects_an_overlong_document()
    {
        var act = () => new Passenger("Ann", "Lee", 3, "Passport", new string('9', Passenger.MaxDocumentLength + 1));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("passenger.document_too_long");
    }

    [Fact]
    public void SetStops_resequences_in_supplied_order_and_replaces_previous()
    {
        var trip = NewTrip();
        trip.SetStops([("Homs", null, null), ("Hama", null, null)]);

        trip.Stops.Select(s => s.Sequence).Should().Equal(1, 2);
        trip.Stops.Select(s => s.Name).Should().Equal("Homs", "Hama");

        // A second call fully replaces — no accumulation, sequence restarts at 1.
        trip.SetStops([("Hama", null, null)]);
        trip.Stops.Should().ContainSingle();
        trip.Stops.Single().Sequence.Should().Be(1);
        trip.Stops.Single().Name.Should().Be("Hama");
    }

    [Fact]
    public void SetStops_is_blocked_on_a_finished_trip()
    {
        var trip = NewTrip();
        trip.Start();
        trip.Complete();

        var act = () => trip.SetStops([("Homs", null, null)]);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("trip.not_editable");
    }

    [Fact]
    public void TripStop_rejects_departure_before_arrival()
    {
        var arrival = DateTimeOffset.UtcNow;
        var act = () => new TripStop(1, "Homs", arrival, arrival.AddMinutes(-5));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("trip_stop.time_invalid");
    }
}
