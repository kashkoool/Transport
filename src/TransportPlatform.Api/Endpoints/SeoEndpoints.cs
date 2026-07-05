using TransportPlatform.Api.Security;
using TransportPlatform.Application.Common;
using TransportPlatform.Application.Seo;

namespace TransportPlatform.Api.Endpoints;

/// <summary>
/// Public, anonymous SEO data feeds (routes, route detail, cities, sitemap) that drive the web app's
/// prerendered landing pages and search-engine crawling. All reads are cacheable and rate-limited on
/// the public-read tier.
/// </summary>
public static class SeoEndpoints
{
    // These reads change slowly (new trips scheduled, companies activated); an hour of edge/CDN
    // caching sheds crawler load without serving meaningfully stale route lists.
    private const string CacheControl = "public, max-age=3600";

    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/seo").WithTags("Seo")
            .RequireRateLimiting(RateLimitPolicies.PublicRead);

        // All distinct routes with an upcoming trip — sitemap/prerender source + routes index page.
        group.MapGet("/routes", async (HttpResponse response, ListSeoRoutesHandler handler, CancellationToken ct) =>
        {
            response.Headers.CacheControl = CacheControl;
            return Results.Ok(await handler.HandleAsync(ct));
        })
        .WithName("ListSeoRoutes")
        .WithSummary("Distinct routes with upcoming trips (count + cheapest fare), most popular first.");

        // Landing-page detail for one route. 404 (NotFoundException) when the slug has no upcoming trips.
        group.MapGet("/routes/{slug}", async (
            HttpResponse response, string slug, GetSeoRouteDetailHandler handler, CancellationToken ct) =>
        {
            if (!Slug.IsValid(slug))
                throw new NotFoundException("Route", slug);
            response.Headers.CacheControl = CacheControl;
            return Results.Ok(await handler.HandleAsync(slug, ct));
        })
        .WithName("GetSeoRouteDetail")
        .WithSummary("Route landing-page detail: fare, companies, avg duration and next departures.");

        // Cities on the network with how many distinct routes touch each.
        group.MapGet("/cities", async (HttpResponse response, ListSeoCitiesHandler handler, CancellationToken ct) =>
        {
            response.Headers.CacheControl = CacheControl;
            return Results.Ok(await handler.HandleAsync(ct));
        })
        .WithName("ListSeoCities")
        .WithSummary("Distinct cities on upcoming routes with their route counts.");

        // sitemap.xml for crawlers. Base URL comes from config (Seo:PublicBaseUrl); falls back to the
        // request's own scheme+host so it is correct even if config is unset.
        group.MapGet("/sitemap.xml", async (
            HttpRequest request, HttpResponse response, IConfiguration config,
            BuildSitemapHandler handler, CancellationToken ct) =>
        {
            var baseUrl = config["Seo:PublicBaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = $"{request.Scheme}://{request.Host}";

            response.Headers.CacheControl = CacheControl;
            var xml = await handler.HandleAsync(baseUrl, ct);
            return Results.Text(xml, "application/xml");
        })
        .WithName("SeoSitemap")
        .WithSummary("XML sitemap listing the site root, routes index and every route/city page.");

        return app;
    }
}
