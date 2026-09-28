using System.Net;
using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Leads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Primitives;

namespace KastomniMebli.Tests;

public class AttributionTests
{
    private static Attribution Parse(params (string Key, string Value)[] fields) =>
        Attribution.From(new FormCollection(fields.ToDictionary(f => f.Key, f => new StringValues(f.Value))));

    [Theory]
    [InlineData("Instagram", null, "instagram")]
    [InlineData(null, "l.instagram.com", "instagram")]
    [InlineData(null, "https://www.google.com.ua/search?q=шафа", "google")]
    [InlineData(null, "lm.facebook.com", "facebook")]
    [InlineData(null, "t.me", "telegram")]
    [InlineData(null, "zviahel-forum.com.ua", "zviahel-forum.com.ua")]
    [InlineData(null, null, "direct")]
    public void Channel_from_utm_or_referrer(string? source, string? referrer, string expected)
    {
        var fields = new List<(string, string)>();
        if (source is not null) fields.Add(("utm_source", source));
        if (referrer is not null) fields.Add(("ref", referrer));

        Assert.Equal(expected, Parse([.. fields]).Channel);
    }

    [Fact]
    public void Junk_is_cleaned()
    {
        var a = Parse(("utm_source", "<script>alert(1)</script>"), ("utm_campaign", new string('x', 300)), ("ref", "not a host!"));

        Assert.Equal("scriptalert1script", a.Source);
        Assert.Equal(Attribution.MaxLength, a.Campaign!.Length);
        Assert.Null(a.Referrer);
    }

    [Fact]
    public async Task Lead_with_utm_becomes_order_with_channel()
    {
        await using var f = new SiteFactory();
        var client = f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        var response = await client.PostAsync(LeadEndpoints.Path, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["name"] = "Олена",
            ["phone"] = "0671234567",
            ["utm_source"] = "instagram",
            ["utm_campaign"] = "kuhni_zhovten",
            ["ref"] = "l.instagram.com",
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lead = Assert.Single(f.Leads());
        Assert.Equal("instagram", lead.UtmSource);
        Assert.Equal("l.instagram.com", lead.Referrer);
        var order = Assert.Single(f.Orders());
        Assert.Equal("instagram", order.Channel);
        Assert.Equal("kuhni_zhovten", order.Campaign);
        Assert.Contains("instagram · kuhni_zhovten", KastomniMebli.Web.Notifications.TelegramMessage.ForLead(lead, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Dashboard_groups_site_orders_by_channel()
    {
        Order Site(int id, string? channel, decimal paid) => new()
        {
            Id = id, Kind = OrderKinds.Retail, Source = "site", Channel = channel, Status = OrderStatuses.New,
            CreatedAt = new DateTime(2026, 9, 5, 10, 0, 0, DateTimeKind.Utc), Client = new Client(),
            Payments = paid == 0 ? [] : [new Payment { Kind = "advance", Amount = paid, PaidOn = new DateOnly(2026, 9, 6) }],
        };

        var r = DashboardCalc.Build([Site(1, "instagram", 20_000m), Site(2, "instagram", 0), Site(3, null, 5_000m)], [], 2026, 9, ["board"]);

        var ig = r.SiteChannels.Single(c => c.Channel == "instagram");
        Assert.Equal(2, ig.Leads);
        Assert.Equal(20_000m, ig.IncomeInMonth);
        Assert.Equal(5_000m, r.SiteChannels.Single(c => c.Channel == "direct").IncomeInMonth);
        Assert.Equal("instagram", r.SiteChannels[0].Channel);
    }
}
