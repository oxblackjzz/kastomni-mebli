using System.Net;
using KastomniMebli.Web.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace KastomniMebli.Tests;

public class SeoTests
{
    private static SiteFactory WithBaseUrl() => new()
    {
        ExtraServices = s => s.Configure<SiteSettings>(o =>
        {
            o.BaseUrl = "https://mebli.example";
            o.Phone = "067 123 45 67";
            o.Telegram = "kastomni";
        }),
    };

    [Fact]
    public async Task Robots_hides_crm_and_points_to_sitemap()
    {
        await using var f = WithBaseUrl();
        var robots = await f.CreateClient().GetStringAsync("/robots.txt");

        Assert.Contains("Disallow: /crm", robots);
        Assert.Contains("Sitemap: https://mebli.example/sitemap.xml", robots);
    }

    [Fact]
    public async Task Sitemap_lists_pages()
    {
        await using var f = WithBaseUrl();
        var xml = await f.CreateClient().GetStringAsync("/sitemap.xml");

        foreach (var page in SeoEndpoints.StaticPages)
            Assert.Contains($"<loc>https://mebli.example{(page == "/" ? "/" : page)}</loc>", xml);
    }

    [Fact]
    public async Task Home_has_business_markup_preview_and_messengers()
    {
        await using var f = WithBaseUrl();
        var html = await f.CreateClient().GetStringAsync("/");

        Assert.Contains("application/ld&#x2B;json", html);   // «+» в атрибуті кодується — це коректний HTML
        Assert.Contains("\"@type\":\"HomeAndConstructionBusiness\"", html);
        Assert.Contains("\"telephone\":\"+380671234567\"", html);
        Assert.Contains("og:image\" content=\"https://mebli.example/og.png\"", html);
        Assert.Contains("rel=\"canonical\" href=\"https://mebli.example/\"", html);
        Assert.Contains("viber://chat?number=%2B380671234567", html);
        Assert.Contains("https://t.me/kastomni", html);
        Assert.Contains("/konfidentsiinist", html);
    }

    [Fact]
    public async Task Privacy_page_renders()
    {
        await using var f = new SiteFactory();
        var response = await f.CreateClient().GetAsync("/konfidentsiinist");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Про захист персональних даних", await response.Content.ReadAsStringAsync());
    }
}
