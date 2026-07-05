using System.Security;
using System.Text;

namespace TransportPlatform.Application.Seo;

/// <summary>
/// Builds a sitemap.xml <c>urlset</c> for the public site: the root, the routes index, one
/// <c>/bus/{slug}</c> per route and one <c>/city/{slug}</c> per city. The base URL is supplied by the
/// caller (config or request); every emitted value is XML-escaped and each URL gets a daily changefreq.
/// </summary>
public sealed class BuildSitemapHandler(ListSeoRoutesHandler routes, ListSeoCitiesHandler cities)
{
    public async Task<string> HandleAsync(string baseUrl, CancellationToken ct)
    {
        var trimmedBase = baseUrl.TrimEnd('/');
        var routeList = await routes.HandleAsync(ct);
        var cityList = await cities.HandleAsync(ct);

        var sb = new StringBuilder(1024);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        sb.Append("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");

        AppendUrl(sb, trimmedBase, "/");
        AppendUrl(sb, trimmedBase, "/routes");

        foreach (var route in routeList)
            AppendUrl(sb, trimmedBase, $"/bus/{route.Slug}");

        foreach (var city in cityList)
            AppendUrl(sb, trimmedBase, $"/city/{city.Slug}");

        sb.Append("</urlset>");
        return sb.ToString();
    }

    private static void AppendUrl(StringBuilder sb, string baseUrl, string path)
    {
        sb.Append("  <url><loc>")
          .Append(SecurityElement.Escape(baseUrl + path))
          .Append("</loc><changefreq>daily</changefreq></url>\n");
    }
}
