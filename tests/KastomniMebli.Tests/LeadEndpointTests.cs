using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Leads;
using KastomniMebli.Web.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KastomniMebli.Tests;

/// <summary>Сайт цілком, але з базою в пам'яті й підміненим Telegram.</summary>
public sealed class SiteFactory : WebApplicationFactory<Program>
{
    public FakeNotifier Notifier { get; } = new();
    public int RateLimitPerHour { get; init; } = 1000;
    private readonly string _dbName = "leads-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));

            services.RemoveAll<ILeadNotifier>();
            services.AddSingleton<ILeadNotifier>(Notifier);

            services.Configure<LeadOptions>(o => o.RateLimitPerHour = RateLimitPerHour);
        });
    }

    public List<Lead> Leads()
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Leads.AsNoTracking().ToList();
    }
}

public sealed class FakeNotifier : ILeadNotifier
{
    public List<Lead> Sent { get; } = [];
    public bool Fail { get; set; }

    public Task NotifyAsync(Lead lead, CancellationToken ct)
    {
        if (Fail)
            throw new HttpRequestException("Telegram недоступний");
        Sent.Add(lead);
        return Task.CompletedTask;
    }
}

public class LeadEndpointTests
{
    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields) =>
        new(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));

    private static FormUrlEncodedContent ValidForm() => Form(
        ("name", "Олена"),
        ("phone", "067 123 45 67"),
        ("types", "kitchen"),
        ("types", "walk-in"),
        ("location", "Звягель"),
        ("dimensions", "3000"),
        ("comment", "Котел у кутку"),
        ("website", ""));

    private static HttpClient JsonClient(SiteFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    [Fact]
    public async Task Valid_lead_is_saved_and_sent_to_telegram()
    {
        await using var factory = new SiteFactory();
        var response = await JsonClient(factory).PostAsync(LeadEndpoints.Path, ValidForm());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal("Дякуємо, Олена! Заявку отримали. Зателефонуємо, щоб домовитись про замір.", body.GetProperty("message").GetString());

        var lead = Assert.Single(factory.Leads());
        Assert.Equal("Олена", lead.Name);
        Assert.Equal("+380671234567", lead.Phone);
        Assert.Equal(["kitchen", "walk-in"], lead.FurnitureTypes);
        Assert.Equal("site", lead.Source);
        Assert.Equal("new", lead.Status);
        Assert.NotNull(lead.TelegramSentAt);
        Assert.Null(lead.TelegramError);
        Assert.Single(factory.Notifier.Sent);
    }

    [Fact]
    public async Task Lead_is_kept_when_telegram_is_down()
    {
        await using var factory = new SiteFactory();
        factory.Notifier.Fail = true;

        var response = await JsonClient(factory).PostAsync(LeadEndpoints.Path, ValidForm());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lead = Assert.Single(factory.Leads());
        Assert.Null(lead.TelegramSentAt);
        Assert.Contains("Telegram недоступний", lead.TelegramError);
    }

    [Fact]
    public async Task Invalid_lead_returns_field_errors_and_is_not_saved()
    {
        await using var factory = new SiteFactory();

        var response = await JsonClient(factory).PostAsync(LeadEndpoints.Path, Form(("name", ""), ("phone", "123")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("phone", out _));
        Assert.Empty(factory.Leads());
        Assert.Empty(factory.Notifier.Sent);
    }

    [Fact]
    public async Task Honeypot_pretends_success_but_saves_nothing()
    {
        await using var factory = new SiteFactory();

        var response = await JsonClient(factory).PostAsync(LeadEndpoints.Path,
            Form(("name", "Bot"), ("phone", "0671234567"), ("website", "http://spam.example")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(factory.Leads());
        Assert.Empty(factory.Notifier.Sent);
    }

    [Fact]
    public async Task Too_fast_submission_is_rejected()
    {
        await using var factory = new SiteFactory();
        var token = factory.Services.GetRequiredService<FormTiming>().Issue();

        var response = await JsonClient(factory).PostAsync(LeadEndpoints.Path,
            Form(("name", "Іван"), ("phone", "0671234567"), ("t", token)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Leads());
    }

    [Fact]
    public async Task Broken_timing_token_does_not_block_real_people()
    {
        // Напр., сторінку відкрили до редеплою, і ключ шифрування змінився.
        await using var factory = new SiteFactory();

        var response = await JsonClient(factory).PostAsync(LeadEndpoints.Path,
            Form(("name", "Іван"), ("phone", "0671234567"), ("t", "зіпсований-токен")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(factory.Leads());
    }

    [Fact]
    public async Task Rate_limit_blocks_flood_from_one_ip()
    {
        await using var factory = new SiteFactory { RateLimitPerHour = 2 };
        var client = JsonClient(factory);

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
            codes.Add((await client.PostAsync(LeadEndpoints.Path, ValidForm())).StatusCode);

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], codes);
        Assert.Equal(2, factory.Leads().Count);
    }

    [Fact]
    public async Task Without_javascript_form_redirects_back_with_result()
    {
        await using var factory = new SiteFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var ok = await client.PostAsync(LeadEndpoints.Path, ValidForm());
        var invalid = await client.PostAsync(LeadEndpoints.Path, Form(("name", ""), ("phone", "")));

        Assert.Equal("/?zayavka=ok#zayavka", ok.Headers.Location?.OriginalString);
        Assert.Equal("/?zayavka=invalid#zayavka", invalid.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Home_page_renders_landing_and_form()
    {
        await using var factory = new SiteFactory();

        var html = await factory.CreateClient().GetStringAsync("/");

        Assert.Contains("Меблі, які стають рівно у&nbsp;вашу нішу", html);
        Assert.Contains("id=\"lead-form\"", html);
        Assert.Contains("name=\"website\"", html);
        Assert.Contains("Звягель та район", html);
        Assert.Contains("Скільки це коштує?", html);
    }
}
