using System.Net;
using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Crm.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace KastomniMebli.Tests;

public class PortfolioTests
{
    private static readonly CurrentUser Admin = new(0, "a", Roles.Admin);

    private static async Task<int> AddWork(PortfolioService p, string title, bool published = true, bool onHome = false, bool photo = true, int sort = 0)
    {
        var id = await p.CreateAsync(Admin, new PortfolioInput { Title = title, Category = "kitchen", IsPublished = published, OnHome = onHome, SortOrder = sort });
        if (photo)
        {
            using var large = new MemoryStream([0xFF, 0xD8, 1, 2, 3]);
            using var thumb = new MemoryStream([0xFF, 0xD8, 4]);
            await p.AddPhotoAsync(Admin, id, large, thumb);
        }
        return id;
    }

    [Fact]
    public async Task Home_shows_featured_first_then_newest_only_published_with_photos()
    {
        await using var f = new SiteFactory();
        using var scope = f.Services.CreateScope();
        var p = scope.ServiceProvider.GetRequiredService<PortfolioService>();

        var old = await AddWork(p, "Стара");
        var draft = await AddWork(p, "Чернетка", published: false);
        var noPhoto = await AddWork(p, "Без фото", photo: false);
        var featured = await AddWork(p, "Вибрана", onHome: true);
        for (var i = 0; i < 6; i++)
            await AddWork(p, "Нова " + i);

        var cards = await p.HomeCardsAsync();

        Assert.Equal(PortfolioService.HomeSlots, cards.Count);
        Assert.Equal(featured, cards[0].Id);
        Assert.DoesNotContain(cards, c => c.Id == draft || c.Id == noPhoto);
        Assert.All(cards, c => Assert.NotNull(c.CoverPhotoId));
    }

    [Fact]
    public async Task Draft_photos_are_not_public()
    {
        await using var f = new SiteFactory();
        int draftPhoto, publicPhoto;
        using (var scope = f.Services.CreateScope())
        {
            var p = scope.ServiceProvider.GetRequiredService<PortfolioService>();
            var draft = await AddWork(p, "Чернетка", published: false);
            var pub = await AddWork(p, "На сайті");
            draftPhoto = (await p.GetAsync(Admin, draft)).Photos[0].Id;
            publicPhoto = (await p.GetAsync(Admin, pub)).Photos[0].Id;
        }

        var client = f.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/roboty/foto/{publicPhoto}/t")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/roboty/foto/{draftPhoto}/l")).StatusCode);
    }

    [Fact]
    public async Task Pages_render_with_placeholders_and_works()
    {
        await using var f = new SiteFactory();
        var client = f.CreateClient();

        var empty = await client.GetStringAsync("/");
        Assert.Equal(PortfolioService.HomeSlots, CountOf(empty, "class=\"work-slot\""));

        int workId;
        using (var scope = f.Services.CreateScope())
            workId = await AddWork(scope.ServiceProvider.GetRequiredService<PortfolioService>(), "Кухня під котел");

        var home = await client.GetStringAsync("/");
        Assert.Equal(PortfolioService.HomeSlots - 1, CountOf(home, "class=\"work-slot\""));
        Assert.Contains("Кухня під котел", await client.GetStringAsync("/roboty"));
        Assert.Contains("Кухня під котел", await client.GetStringAsync($"/roboty/{workId}"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/roboty/99999")).StatusCode);
    }

    [Fact]
    public async Task Installer_cannot_edit_portfolio()
    {
        var ctx = new CrmTestContext();
        var p = new PortfolioService(ctx.Db, null!, ctx.Time);

        await Assert.ThrowsAsync<CrmForbiddenException>(() =>
            p.CreateAsync(new CurrentUser(5, "Кум", Roles.Installer), new PortfolioInput { Title = "x", Category = "kitchen" }));
    }

    private static int CountOf(string text, string what)
    {
        var count = 0;
        for (var i = text.IndexOf(what, StringComparison.Ordinal); i >= 0; i = text.IndexOf(what, i + 1, StringComparison.Ordinal))
            count++;
        return count;
    }
}
