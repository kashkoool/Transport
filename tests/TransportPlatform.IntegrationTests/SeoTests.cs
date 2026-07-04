using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace TransportPlatform.IntegrationTests;

/// <summary>
/// The public SEO feeds surface the seeded Damascus→Latakia route: the routes list, the route
/// detail (resolved from its slug), the cities list, and a valid sitemap.xml linking the route page.
/// </summary>
public sealed class SeoTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Routes_list_includes_the_seeded_route_with_its_slug()
    {
        await factory.SeedTripAsync();
        var client = factory.CreateClient();

        var routes = await client.GetFromJsonAsync<List<RouteSummaryDto>>("/api/seo/routes", Json);

        routes.Should().NotBeNull();
        routes!.Should().Contain(r =>
            r.Origin == "Damascus" && r.Destination == "Latakia" && r.Slug == "damascus-to-latakia");
        routes.Single(r => r.Slug == "damascus-to-latakia").TripCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Route_detail_resolves_the_slug_and_reports_upcoming_trips()
    {
        await factory.SeedTripAsync();
        var client = factory.CreateClient();

        var detail = await client.GetFromJsonAsync<RouteDetailDto>("/api/seo/routes/damascus-to-latakia", Json);

        detail.Should().NotBeNull();
        detail!.Origin.Should().Be("Damascus");
        detail.Destination.Should().Be("Latakia");
        detail.UpcomingCount.Should().BeGreaterThan(0);
        detail.Companies.Should().NotBeEmpty();
        detail.Next.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Unknown_route_slug_returns_404()
    {
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/seo/routes/nowhere-to-elsewhere");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cities_list_includes_the_seeded_city()
    {
        await factory.SeedTripAsync();
        var client = factory.CreateClient();

        var cities = await client.GetFromJsonAsync<List<CitySummaryDto>>("/api/seo/cities", Json);

        cities.Should().NotBeNull();
        cities!.Should().Contain(c => c.Name == "Damascus" && c.Slug == "damascus");
    }

    [Fact]
    public async Task Sitemap_returns_xml_listing_the_route_page()
    {
        await factory.SeedTripAsync();
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/seo/sitemap.xml");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("application/xml");
        var xml = await resp.Content.ReadAsStringAsync();
        xml.Should().Contain("<urlset");
        xml.Should().Contain("/bus/damascus-to-latakia");
    }

    private sealed record RouteSummaryDto(
        string Origin, string Destination, string Slug, int TripCount, decimal MinPrice, string Currency);

    private sealed record NextDepartureDto(
        DateTimeOffset DepartureUtc, DateTimeOffset ArrivalUtc, decimal Price, string Currency, string CompanyName);

    private sealed record RouteDetailDto(
        string Origin, string Destination, string Slug, decimal MinPrice, string Currency,
        int UpcomingCount, string[] Companies, int? AvgDurationMinutes, NextDepartureDto[] Next);

    private sealed record CitySummaryDto(string Name, string Slug, int RouteCount);
}
