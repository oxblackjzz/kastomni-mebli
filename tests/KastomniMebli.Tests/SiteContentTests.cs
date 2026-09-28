using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace KastomniMebli.Tests;

public class SiteContentTests
{
    private static readonly CurrentUser Admin = new(0, "a", Roles.Admin);

    [Fact]
    public async Task Changes_from_crm_appear_on_site_immediately()
    {
        await using var f = new SiteFactory();
        var client = f.CreateClient();
        Assert.DoesNotContain("Чи робите меблі на балкон?", await client.GetStringAsync("/"));

        using (var scope = f.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<SiteContentService>().SaveAsync(Admin, new SiteContent
            {
                Phone = "067 555 44 33",
                Faq = [new FaqItem { Question = "Чи робите меблі на балкон?", Answer = "Так." }],
                Areas = ["Звягель", " ", "Житомир"],
            });
        }

        var html = await client.GetStringAsync("/");
        Assert.Contains("Чи робите меблі на балкон?", html);
        Assert.Contains("tel:&#x2B;380675554433", html);
        Assert.Contains("Звягель · Житомир", html);
        // Команда не змінювалась — лишились типові ролі з конфігурації.
        Assert.Contains("Конструктор", html);
    }

    [Fact]
    public async Task Invalid_values_rejected_and_reset_restores_defaults()
    {
        await using var f = new SiteFactory();
        using var scope = f.Services.CreateScope();
        var content = scope.ServiceProvider.GetRequiredService<SiteContentService>();
        var defaultFaq = content.Effective.Faq.Count;

        await Assert.ThrowsAsync<CrmException>(() => content.SaveAsync(Admin, new SiteContent { Phone = "123" }));
        await Assert.ThrowsAsync<CrmException>(() => content.SaveAsync(Admin, new SiteContent { Telegram = "a b" }));
        await Assert.ThrowsAsync<CrmForbiddenException>(() => content.SaveAsync(new CurrentUser(1, "k", Roles.Installer), new SiteContent()));

        await content.SaveAsync(Admin, new SiteContent { Faq = [] });
        Assert.Empty(content.Effective.Faq);

        await content.ResetAsync(Admin);
        Assert.Equal(defaultFaq, content.Effective.Faq.Count);
    }
}
