using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KastomniMebli.Web.B2b;
using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KastomniMebli.Tests;

public class B2bTests
{
    private static readonly DateOnly Today = new(2026, 9, 15);
    private const long MaxFile = 20 * 1024 * 1024;

    private static IFormFile File(string name, long size) =>
        new FormFile(new MemoryStream(new byte[Math.Min(size, 16)]), 0, size, "files", name);

    [Fact]
    public void Valid_form_normalizes_phone_and_telegram()
    {
        var form = new B2bForm
        {
            Name = "  Цех   «Дуб» ",
            Phone = "067 123 45 67",
            Telegram = "https://t.me/dub_mebli",
            Types = ["kitchen", "bogus"],
            Due = "2026-10-01",
            Files = [File("kuhnia.dwg", 1000), File("foto.jpg", 2000)],
        };

        var r = form.Validate(Today, MaxFile);

        Assert.True(r.IsValid);
        Assert.Equal("Цех «Дуб»", r.Request!.Name);
        Assert.Equal("+380671234567", r.Request.Phone);
        Assert.Equal("dub_mebli", r.Request.Telegram);
        Assert.Equal(["kitchen"], r.Request.Types);
        Assert.Equal(new DateOnly(2026, 10, 1), r.Request.Due);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("phone")]
    [InlineData("telegram")]
    [InlineData("due")]
    public void Invalid_fields_are_reported(string field)
    {
        var form = new B2bForm
        {
            Name = field == "name" ? "" : "Цех",
            Phone = field == "phone" ? "123" : "0671234567",
            Telegram = field == "telegram" ? "ab" : null,
            Due = field == "due" ? "2026-09-01" : null,   // минула дата
        };

        Assert.Contains(field, form.Validate(Today, MaxFile).Errors.Keys);
    }

    [Theory]
    [InlineData("virus.exe", 100)]
    [InlineData("big.pdf", 21L * 1024 * 1024)]
    public void Bad_files_are_rejected(string name, long size)
    {
        var form = new B2bForm { Name = "Цех", Phone = "0671234567", Files = [File(name, size)] };

        Assert.Contains("files", form.Validate(Today, MaxFile).Errors.Keys);
    }

    [Fact]
    public void Too_many_files_rejected()
    {
        var files = Enumerable.Range(0, B2bForm.MaxFiles + 1).Select(i => File($"f{i}.jpg", 10)).ToList();
        var form = new B2bForm { Name = "Цех", Phone = "0671234567", Files = files };

        Assert.Contains("files", form.Validate(Today, MaxFile).Errors.Keys);
    }

    [Fact]
    public async Task Submitted_request_becomes_b2b_order_with_files_and_notifies()
    {
        await using var f = new SiteFactory();
        var client = f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        using var content = new MultipartFormDataContent
        {
            { new StringContent("Цех Дуб"), "name" },
            { new StringContent("050 111 22 33"), "phone" },
            { new StringContent("@dub_mebli"), "telegram" },
            { new StringContent("kitchen"), "types" },
            { new StringContent("Кухня 3 м, котел справа"), "comment" },
        };
        var file = new ByteArrayContent("%PDF-1.4 заміри"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "files", "zamiry.pdf");

        var response = await client.PostAsync(B2bEndpoints.SubmitPath, content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ok").GetBoolean());

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.Include(o => o.Client).Include(o => o.Files).Include(o => o.Shares).SingleAsync();
        Assert.Equal(OrderKinds.B2b, order.Kind);
        Assert.Equal(OrderStatuses.New, order.Status);
        Assert.Equal("furniture_maker", order.Source);
        Assert.Equal("dub_mebli", order.Client.Telegram);
        Assert.Empty(order.Shares);
        var saved = Assert.Single(order.Files);
        Assert.Equal("zamiry.pdf", saved.FileName);
        Assert.Equal("brief", saved.Kind);
        Assert.True(System.IO.File.Exists(Path.Combine(f.FilesRoot, saved.StoragePath)));
        Assert.Contains(f.Telegram.Sent, m => m.Text.Contains("B2B") && m.Text.Contains("Цех Дуб"));
    }

    [Fact]
    public async Task Page_renders_and_hides_empty_prices()
    {
        await using var f = new SiteFactory();
        var html = await f.CreateClient().GetStringAsync(B2bEndpoints.PagePath);

        Assert.Contains("id=\"b2b-form\"", html);
        Assert.Contains("Ціну називаю після того, як перегляну завдання.", html);
        Assert.DoesNotContain("Від вартості матеріалу", html);

        using (var scope = f.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SettingsService>()
                .SetB2bPricingAsync(new CurrentUser(0, "a", Roles.Admin), new B2bPricing { MaterialsPercent = 5m });

        html = await f.CreateClient().GetStringAsync(B2bEndpoints.PagePath);
        Assert.Contains("Від вартості матеріалу", html);
        Assert.Contains("5%", html);
    }

    [Fact]
    public void B2b_status_flow_and_completion()
    {
        var order = new Order { Kind = OrderKinds.B2b, Status = OrderStatuses.New };
        var t = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        Assert.Equal(OrderStatuses.InWork, OrderStatuses.Next(OrderKinds.B2b, OrderStatuses.New));
        Assert.Null(OrderStatuses.Next(OrderKinds.B2b, OrderStatuses.Paid));

        OrderService.ApplyStatus(order, OrderStatuses.InWork, 1, t);
        Assert.Equal(t, order.ApprovedAt);
        Assert.Null(order.MeasuredAt);

        OrderService.ApplyStatus(order, OrderStatuses.Delivered, 1, t.AddDays(2));
        Assert.Null(order.CompletedAt);
        Assert.True(OrderStatuses.IsOpen(order.Status));

        OrderService.ApplyStatus(order, OrderStatuses.Paid, 1, t.AddDays(3));
        Assert.Equal(t.AddDays(3), order.CompletedAt);
        Assert.False(OrderStatuses.IsOpen(order.Status));
    }

    [Fact]
    public async Task Retail_statuses_are_rejected_for_b2b_order()
    {
        var ctx = new CrmTestContext();
        var id = await ctx.Orders.CreateAsync(CrmTestContext.Admin, new NewOrderInput { Kind = OrderKinds.B2b, ClientName = "Цех", Source = "furniture_maker" });

        await Assert.ThrowsAsync<CrmException>(() => ctx.Orders.ChangeStatusAsync(CrmTestContext.Admin, id, OrderStatuses.Installation));
        await ctx.Orders.ChangeStatusAsync(CrmTestContext.Admin, id, OrderStatuses.InWork);

        Assert.Single(await ctx.Orders.ListAsync(CrmTestContext.Admin, "open", kind: OrderKinds.B2b));
        Assert.Empty(await ctx.Orders.ListAsync(CrmTestContext.Admin, "open"));
    }

    [Fact]
    public void Dashboard_keeps_b2b_separate()
    {
        var retail = new Order
        {
            Id = 1, Kind = OrderKinds.Retail, Source = "site", Status = OrderStatuses.New,
            CreatedAt = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc), Client = new Client(),
            Payments = [new Payment { Kind = "advance", Amount = 10_000m, PaidOn = new DateOnly(2026, 9, 3) }],
        };
        var b2b = new Order
        {
            Id = 2, Kind = OrderKinds.B2b, Source = "furniture_maker", Status = OrderStatuses.Paid, ContractAmount = 3_000m,
            CreatedAt = new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc), CompletedAt = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc),
            Client = new Client(),
            Payments = [new Payment { Kind = "extra", Amount = 3_000m, PaidOn = new DateOnly(2026, 9, 10) }],
        };

        var r = DashboardCalc.Build([retail, b2b], [], 2026, 9, ["board"]);

        Assert.Equal(10_000m, r.Cash.Income);                 // лише гроші команди
        Assert.Equal(1, r.Funnel.Leads);
        Assert.Equal(0m, r.Sources.Single(s => s.Source == "furniture_maker").IncomeInMonth);
        Assert.Equal(3_000m, r.B2b.IncomeInMonth);
        Assert.Equal(1, r.B2b.PaidInMonth);
        Assert.Equal(0m, r.B2b.Unpaid);
    }
}
