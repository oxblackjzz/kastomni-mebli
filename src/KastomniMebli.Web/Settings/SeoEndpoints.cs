using System.Text;
using System.Xml;
using KastomniMebli.Web.Crm;
using Microsoft.Extensions.Options;

namespace KastomniMebli.Web.Settings;

/// <summary>robots.txt і sitemap.xml — щоб Google знайшов усі сторінки й не лазив у CRM.</summary>
public static class SeoEndpoints
{
    public static readonly string[] StaticPages = ["/", "/roboty", "/dlya-meblyariv", "/konfidentsiinist"];

    public static void MapSeoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/robots.txt", (IOptions<SiteSettings> site) =>
        {
            var sb = new StringBuilder()
                .AppendLine("User-agent: *")
                .AppendLine("Disallow: /crm")
                .AppendLine("Disallow: /media/");
            if (site.Value.AbsoluteUrl("/sitemap.xml") is { } sitemap)
                sb.AppendLine($"Sitemap: {sitemap}");
            return Results.Text(sb.ToString(), "text/plain; charset=utf-8");
        });

        app.MapGet("/sitemap.xml", async (IOptions<SiteSettings> site, PortfolioService portfolio) =>
        {
            if (string.IsNullOrWhiteSpace(site.Value.BaseUrl))
                return Results.NotFound();

            var urls = StaticPages.Select(p => site.Value.AbsoluteUrl(p)!).ToList();
            urls.AddRange((await portfolio.PublishedCardsAsync()).Select(w => site.Value.AbsoluteUrl($"/roboty/{w.Id}")!));

            var sb = new StringBuilder();
            using (var xml = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true }))
            {
                xml.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
                foreach (var url in urls)
                {
                    xml.WriteStartElement("url");
                    xml.WriteElementString("loc", url);
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }
            return Results.Text("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + sb, "application/xml; charset=utf-8");
        });
    }
}
