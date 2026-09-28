using System.Net;
using System.Text;
using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace KastomniMebli.Tests;

public class AccountingTests
{
    private static MoneyInput Pay(decimal amount, string kind = "advance") =>
        new() { Kind = kind, Amount = amount, Method = "cash", Date = new DateOnly(2026, 9, 10) };

    [Fact]
    public async Task Money_changes_are_logged_with_who_and_how_much()
    {
        var ctx = new CrmTestContext();
        var brat = await ctx.UserAsync("brat", Roles.Manager, "Брат");
        var id = await ctx.Orders.CreateAsync(brat, new NewOrderInput { ClientName = "А", Source = "brother" });

        await ctx.Orders.AddPaymentAsync(brat, id, Pay(20_000m));
        var paymentId = (await ctx.Orders.GetAsync(brat, id)).Payments.Single().Id;
        await ctx.Orders.DeletePaymentAsync(brat, paymentId);
        await ctx.Orders.UpdateDetailsAsync(brat, id, new OrderDetailsInput { Source = "brother", ContractAmount = 45_000m });
        await ctx.Orders.AddExpenseAsync(brat, id, new MoneyInput { Kind = "board", Amount = 9_000m, Date = new DateOnly(2026, 9, 11) });

        var log = await ctx.Orders.AuditAsync(brat, id);
        Assert.Equal([AuditActions.ExpenseAdded, AuditActions.ContractChanged, AuditActions.PaymentDeleted, AuditActions.PaymentAdded],
            log.Select(a => a.Action));
        Assert.All(log, a => Assert.Equal("Брат", a.UserName));
        Assert.Equal(20_000m, log.Single(a => a.Action == AuditActions.PaymentDeleted).Amount);
        // Тисячі розділяються нерозривним пробілом (українська локаль) — для порівняння зводимо до звичайного.
        var details = log.Single(a => a.Action == AuditActions.ContractChanged).Details.Replace(' ', ' ').Replace(' ', ' ');
        Assert.Contains("45 000 грн", details);
    }

    [Fact]
    public async Task Pay_all_marks_owed_shares_of_completed_orders_only()
    {
        var ctx = new CrmTestContext();
        var kum = await ctx.UserAsync("kum", Roles.Installer, "Кум");
        async Task<int> OrderWithShare(decimal contract, bool done)
        {
            var id = await ctx.Orders.CreateAsync(CrmTestContext.Admin, new NewOrderInput { ClientName = "А", Source = "brother", ContractAmount = contract });
            await ctx.Orders.AddShareAsync(CrmTestContext.Admin, id, kum.Id, ShareBasis.ContractPercent, 10m);
            if (done)
                await ctx.Orders.ChangeStatusAsync(CrmTestContext.Admin, id, OrderStatuses.Done);
            return id;
        }
        await OrderWithShare(30_000m, done: true);
        await OrderWithShare(50_000m, done: true);
        var open = await OrderWithShare(80_000m, done: false);

        var (count, total) = await ctx.Orders.PayAllOwedAsync(CrmTestContext.Admin, kum.Id, new DateOnly(2026, 9, 20));

        Assert.Equal(2, count);
        Assert.Equal(8_000m, total);
        Assert.Null((await ctx.Orders.GetAsync(CrmTestContext.Admin, open)).Shares.Single().PaidAmount);
        Assert.Equal(2, (await ctx.Orders.AuditAsync(CrmTestContext.Admin)).Count(a => a.Action == AuditActions.SharePaid));
        Assert.Equal((0, 0m), await ctx.Orders.PayAllOwedAsync(CrmTestContext.Admin, kum.Id, new DateOnly(2026, 9, 21)));
    }

    [Fact]
    public async Task Cancel_reason_is_stored_logged_and_cleared_on_reopen()
    {
        var ctx = new CrmTestContext();
        var id = await ctx.Orders.CreateAsync(CrmTestContext.Admin, new NewOrderInput { ClientName = "А", Source = "brother" });

        await Assert.ThrowsAsync<CrmException>(() => ctx.Orders.ChangeStatusAsync(CrmTestContext.Admin, id, OrderStatuses.Cancelled, "bogus"));
        await ctx.Orders.ChangeStatusAsync(CrmTestContext.Admin, id, OrderStatuses.Cancelled, "expensive", "хотіли дешевше на 20%");

        var order = await ctx.Orders.GetAsync(CrmTestContext.Admin, id);
        Assert.Equal("expensive", order.CancelReason);
        Assert.Equal("хотіли дешевше на 20%", order.CancelNote);
        Assert.Contains("Дорого", (await ctx.Orders.AuditAsync(CrmTestContext.Admin, id)).Single().Details);

        await ctx.Orders.ChangeStatusAsync(CrmTestContext.Admin, id, OrderStatuses.New);
        Assert.Null((await ctx.Orders.GetAsync(CrmTestContext.Admin, id)).CancelReason);
    }

    [Fact]
    public void Dashboard_company_expenses_ads_and_lost_reasons()
    {
        var site = new Order
        {
            Id = 1, Kind = OrderKinds.Retail, Source = "site", Status = OrderStatuses.New, Client = new Client(),
            CreatedAt = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc),
            Payments = [new Payment { Kind = "advance", Amount = 10_000m, PaidOn = new DateOnly(2026, 9, 4) }],
        };
        var site2 = new Order { Id = 2, Kind = OrderKinds.Retail, Source = "site", Status = OrderStatuses.Cancelled, CancelReason = "expensive",
            CreatedAt = new DateTime(2026, 9, 5, 10, 0, 0, DateTimeKind.Utc), CancelledAt = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc), Client = new Client() };
        var company = new List<CompanyExpense>
        {
            new() { Category = Catalog.AdvertisingCategory, Amount = 2_000m, SpentOn = new DateOnly(2026, 9, 1) },
            new() { Category = "fuel", Amount = 500m, SpentOn = new DateOnly(2026, 9, 2) },
            new() { Category = Catalog.AdvertisingCategory, Amount = 9_999m, SpentOn = new DateOnly(2026, 8, 31) },   // інший місяць
        };

        var r = DashboardCalc.Build([site, site2], [], 2026, 9, ["board"], company);

        Assert.Equal(2_500m, r.Cash.CompanyExpenses);
        Assert.Equal(7_500m, r.Cash.Net);                  // 10 000 − 2 500
        Assert.Equal(2_000m, r.Ads.Spent);
        Assert.Equal(1_000m, r.Ads.CostPerLead);           // 2 заявки з сайту
        Assert.Equal(5.0m, r.Ads.Return);                  // 10 000 / 2 000
        var lost = Assert.Single(r.Lost);
        Assert.Equal("Дорого", lost.Label);
        Assert.Equal(Catalog.AdvertisingCategory, r.CompanyExpensesByCategory[0].Category);
    }

    [Fact]
    public async Task Company_expenses_require_money_role_and_are_logged()
    {
        var ctx = new CrmTestContext();
        var service = new CompanyExpenseService(ctx.Db, ctx.Time);

        await service.AddAsync(CrmTestContext.Admin, new MoneyInput { Kind = Catalog.AdvertisingCategory, Amount = 1_500m, Date = new DateOnly(2026, 9, 1), Note = "Instagram" });
        await Assert.ThrowsAsync<CrmForbiddenException>(() => service.ListAsync(new CurrentUser(9, "k", Roles.Installer), 2026, 9));
        await Assert.ThrowsAsync<CrmException>(() => service.AddAsync(CrmTestContext.Admin, new MoneyInput { Kind = "gift", Amount = 1m, Date = new DateOnly(2026, 9, 1) }));

        Assert.Single(await service.ListAsync(CrmTestContext.Admin, 2026, 9));
        Assert.Empty(await service.ListAsync(CrmTestContext.Admin, 2026, 10));
        Assert.Contains(await ctx.Orders.AuditAsync(CrmTestContext.Admin), a => a.Action == AuditActions.CompanyExpenseAdded && a.Amount == 1_500m);
    }

    [Fact]
    public void Csv_is_excel_friendly_and_formula_safe()
    {
        var bytes = ExportService.ToCsv([["Клієнт", "Сума"], ["=HYPERLINK(\"x\")", "-1500,00"], ["Олена; Петрівна", "1 000,50"]]);
        var text = Encoding.UTF8.GetString(bytes);

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        Assert.Contains("Клієнт;Сума\r\n", text);
        Assert.Contains("'=HYPERLINK", text);          // формула не виконається
        Assert.Contains(";-1500,00", text);             // від'ємне число лишається числом
        Assert.Contains("\"Олена; Петрівна\"", text);
    }

    [Fact]
    public async Task Export_endpoint_needs_money_role()
    {
        await using var f = new SiteFactory();
        var anonymous = await f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false })
            .GetAsync("/crm/eksport/2026/9/oplaty.csv");
        Assert.NotEqual(HttpStatusCode.OK, anonymous.StatusCode);

        using var scope = f.Services.CreateScope();
        var export = scope.ServiceProvider.GetRequiredService<ExportService>();
        var csv = Encoding.UTF8.GetString(await export.ExportAsync(CrmTestContext.Admin, "zamovlennia", 2026, 9));
        Assert.StartsWith("﻿№;Вид;Клієнт", csv);
    }

    [Fact]
    public async Task Calendar_shows_week_events_installer_sees_only_own()
    {
        var ctx = new CrmTestContext();
        var kum = await ctx.UserAsync("kum", Roles.Installer, "Кум");
        var mine = await ctx.Orders.CreateAsync(CrmTestContext.Admin, new NewOrderInput { ClientName = "Мій", Source = "brother" });
        var other = await ctx.Orders.CreateAsync(CrmTestContext.Admin, new NewOrderInput { ClientName = "Чужий", Source = "brother" });
        await ctx.Orders.SetAssigneeAsync(CrmTestContext.Admin, mine, "installer", kum.Id);
        await ctx.Orders.UpdateDetailsAsync(CrmTestContext.Admin, mine, new OrderDetailsInput { Source = "brother", InstallDateLocal = new DateTime(2026, 9, 16, 9, 0, 0) });
        await ctx.Orders.UpdateDetailsAsync(CrmTestContext.Admin, other, new OrderDetailsInput { Source = "brother", MeasureDateLocal = new DateTime(2026, 9, 14, 18, 30, 0) });
        var monday = new DateOnly(2026, 9, 14);

        var all = await ctx.Orders.CalendarAsync(CrmTestContext.Admin, monday);
        Assert.Equal(["measure", "install"], all.Select(e => e.Kind));
        Assert.Equal(new DateTime(2026, 9, 14, 18, 30, 0), all[0].Local);   // київський час, як ввели

        var own = Assert.Single(await ctx.Orders.CalendarAsync(kum, monday));
        Assert.Equal("Мій", own.ClientName);
        Assert.Empty(await ctx.Orders.CalendarAsync(CrmTestContext.Admin, monday.AddDays(7)));
    }

    [Fact]
    public async Task Manifest_and_health_are_served()
    {
        await using var f = new SiteFactory();
        var client = f.CreateClient();

        Assert.Equal("ok", await client.GetStringAsync("/healthz"));
        var manifest = await client.GetAsync("/crm.webmanifest");
        Assert.Equal(HttpStatusCode.OK, manifest.StatusCode);
        Assert.Contains("\"start_url\": \"/crm\"", await manifest.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/icon-192.png")).StatusCode);
    }
}
