using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace KastomniMebli.Tests;

public class OrderServiceTests
{
    private static Lead NewLead(long id, string phone = "+380671234567") => new()
    {
        Id = id,
        CreatedAt = new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc),
        Name = "Олена",
        Phone = phone,
        PhoneRaw = phone,
        FurnitureTypes = ["kitchen"],
        Location = "Звягель",
        Dimensions = "3000",
        Comment = "Котел у кутку",
    };

    private static async Task<Lead> SaveLead(CrmTestContext ctx, Lead lead)
    {
        await using var db = ctx.Db.CreateDbContext();
        db.Leads.Add(lead);
        await db.SaveChangesAsync();
        return lead;
    }

    [Fact]
    public async Task Lead_becomes_order_with_client()
    {
        var ctx = new CrmTestContext();
        var lead = await SaveLead(ctx, NewLead(1));

        var id = await ctx.Orders.CreateFromLeadAsync(lead);
        var order = await ctx.Orders.GetAsync(CrmTestContext.Admin, id);

        Assert.Equal("2026-001", order.Number);
        Assert.Equal(OrderStatuses.New, order.Status);
        Assert.Equal("site", order.Source);
        Assert.Equal(lead.Id, order.LeadId);
        Assert.Equal(["kitchen"], order.FurnitureTypes);
        Assert.Equal("Олена", order.Client.Name);
        Assert.Equal("+380671234567", order.Client.Phone);
        Assert.Contains("Розміри: 3000", order.Comment);
        Assert.Contains("Котел у кутку", order.Comment);
        Assert.Equal(lead.CreatedAt, order.CreatedAt);
    }

    [Fact]
    public async Task Same_phone_reuses_client_and_same_lead_is_not_duplicated()
    {
        var ctx = new CrmTestContext();
        var first = await SaveLead(ctx, NewLead(1));
        var second = await SaveLead(ctx, NewLead(2));

        var a = await ctx.Orders.CreateFromLeadAsync(first);
        var b = await ctx.Orders.CreateFromLeadAsync(second);
        var again = await ctx.Orders.CreateFromLeadAsync(first);

        Assert.Equal(a, again);
        await using var db = ctx.Db.CreateDbContext();
        Assert.Equal(1, await db.Clients.CountAsync());
        Assert.Equal(2, await db.Orders.CountAsync());
        Assert.Equal("2026-002", (await db.Orders.FindAsync(b))!.Number);
    }

    [Fact]
    public async Task Backfill_creates_orders_for_old_leads()
    {
        var ctx = new CrmTestContext();
        await SaveLead(ctx, NewLead(1, "+380501111111"));
        await SaveLead(ctx, NewLead(2, "+380502222222"));

        Assert.Equal(2, await ctx.Orders.BackfillLeadsAsync());
        Assert.Equal(0, await ctx.Orders.BackfillLeadsAsync());
    }

    [Fact]
    public async Task New_order_copies_share_template()
    {
        var ctx = new CrmTestContext();
        var designer = await ctx.UserAsync("vova", Roles.Admin, "Конструктор");
        await ctx.Settings.SaveTemplateAsync(CrmTestContext.Admin, null, designer.Id, ShareBasis.MaterialsPercent, 5m, true);

        var id = await ctx.Orders.CreateAsync(CrmTestContext.Admin, new NewOrderInput { ClientName = "Іван", Phone = "0501234567", Source = "brother" });
        var order = await ctx.Orders.GetAsync(CrmTestContext.Admin, id);

        var share = Assert.Single(order.Shares);
        Assert.Equal(designer.Id, share.UserId);
        Assert.Equal(ShareBasis.MaterialsPercent, share.Basis);
        Assert.Equal(5m, share.Value);
        Assert.Contains(ctx.Telegram.Sent, m => m.Text.Contains(order.Number));
    }

    [Fact]
    public void Status_milestones_are_recorded()
    {
        var order = new Order { Status = OrderStatuses.New };
        var t1 = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var t2 = t1.AddDays(3);

        OrderService.ApplyStatus(order, OrderStatuses.Drawing, 1, t1);   // перескочили замір
        Assert.Equal(t1, order.MeasuredAt);
        Assert.Null(order.ApprovedAt);

        OrderService.ApplyStatus(order, OrderStatuses.Done, 1, t2);
        Assert.Equal(t1, order.MeasuredAt);   // перша дата не перезаписується
        Assert.Equal(t2, order.ApprovedAt);
        Assert.Equal(t2, order.CompletedAt);

        OrderService.ApplyStatus(order, OrderStatuses.Installation, 1, t2.AddDays(1));  // повернули назад
        Assert.Null(order.CompletedAt);

        OrderService.ApplyStatus(order, OrderStatuses.Cancelled, 1, t2.AddDays(2));
        Assert.NotNull(order.CancelledAt);
        Assert.Equal(4, order.StatusHistory.Count);
        Assert.Equal(OrderStatuses.Installation, order.StatusHistory[^1].FromStatus);
    }

    [Fact]
    public async Task Installer_sees_only_assigned_orders_without_money()
    {
        var ctx = new CrmTestContext();
        var installer = await ctx.UserAsync("kum", Roles.Installer, "Монтажник");
        var mine = await ctx.Orders.CreateAsync(CrmTestContext.Admin, new NewOrderInput { ClientName = "А", Source = "brother", ContractAmount = 50_000m });
        var other = await ctx.Orders.CreateAsync(CrmTestContext.Admin, new NewOrderInput { ClientName = "Б", Source = "brother", ContractAmount = 70_000m });
        await ctx.Orders.SetAssigneeAsync(CrmTestContext.Admin, mine, "installer", installer.Id);
        await ctx.Orders.AddPaymentAsync(CrmTestContext.Admin, mine, new MoneyInput { Kind = "advance", Amount = 20_000m, Method = "cash", Date = new DateOnly(2026, 9, 1) });

        var list = await ctx.Orders.ListAsync(installer, "all");
        var item = Assert.Single(list);
        Assert.Equal(mine, item.Id);
        Assert.Null(item.ContractAmount);

        var order = await ctx.Orders.GetAsync(installer, mine);
        Assert.Null(order.ContractAmount);
        Assert.Empty(order.Payments);
        Assert.Empty(order.Shares);

        await Assert.ThrowsAsync<CrmForbiddenException>(() => ctx.Orders.GetAsync(installer, other));
        await Assert.ThrowsAsync<CrmForbiddenException>(() => ctx.Orders.AddPaymentAsync(installer, mine,
            new MoneyInput { Kind = "extra", Amount = 1m, Method = "cash", Date = new DateOnly(2026, 9, 1) }));
        await Assert.ThrowsAsync<CrmForbiddenException>(() => ctx.Orders.ListClientsAsync(installer, null));
    }

    [Fact]
    public async Task Installer_can_only_finish_own_installation_and_gets_notified()
    {
        var ctx = new CrmTestContext();
        var installer = await ctx.UserAsync("kum", Roles.Installer, "Монтажник");
        await ctx.Users.UpdateAsync(CrmTestContext.Admin, installer.Id, new UserInput("kum", "Монтажник", Roles.Installer, null, "555001"));
        var id = await ctx.Orders.CreateAsync(CrmTestContext.Admin, new NewOrderInput { ClientName = "А", Source = "brother" });
        await ctx.Orders.SetAssigneeAsync(CrmTestContext.Admin, id, "installer", installer.Id);

        await Assert.ThrowsAsync<CrmForbiddenException>(() => ctx.Orders.ChangeStatusAsync(installer, id, OrderStatuses.Done));

        await ctx.Orders.ChangeStatusAsync(CrmTestContext.Admin, id, OrderStatuses.Installation);
        Assert.Contains(ctx.Telegram.Sent, m => m.ChatId == "555001" && m.Text.Contains("Монтаж"));

        await Assert.ThrowsAsync<CrmForbiddenException>(() => ctx.Orders.ChangeStatusAsync(installer, id, OrderStatuses.Cancelled));
        await ctx.Orders.ChangeStatusAsync(installer, id, OrderStatuses.Done);
        Assert.Equal(OrderStatuses.Done, (await ctx.Orders.GetAsync(CrmTestContext.Admin, id)).Status);
    }

    [Fact]
    public async Task Telegram_failure_does_not_break_status_change()
    {
        var ctx = new CrmTestContext();
        ctx.Telegram.Fail = true;
        var id = await ctx.Orders.CreateAsync(CrmTestContext.Admin, new NewOrderInput { ClientName = "А", Source = "brother" });

        await ctx.Orders.ChangeStatusAsync(CrmTestContext.Admin, id, OrderStatuses.Installation);

        Assert.Equal(OrderStatuses.Installation, (await ctx.Orders.GetAsync(CrmTestContext.Admin, id)).Status);
    }

    [Fact]
    public async Task Paid_share_freezes_amount_and_cannot_be_edited()
    {
        var ctx = new CrmTestContext();
        var designer = await ctx.UserAsync("vova", Roles.Admin, "Конструктор");
        var id = await ctx.Orders.CreateAsync(CrmTestContext.Admin, new NewOrderInput { ClientName = "А", Source = "brother", ContractAmount = 40_000m });
        await ctx.Orders.AddShareAsync(CrmTestContext.Admin, id, designer.Id, ShareBasis.ContractPercent, 10m);
        var shareId = (await ctx.Orders.GetAsync(CrmTestContext.Admin, id)).Shares.Single().Id;

        await ctx.Orders.MarkSharePaidAsync(CrmTestContext.Admin, shareId, new DateOnly(2026, 9, 20));
        await ctx.Orders.UpdateDetailsAsync(CrmTestContext.Admin, id, new OrderDetailsInput { Source = "brother", ContractAmount = 50_000m });

        var order = await ctx.Orders.GetAsync(CrmTestContext.Admin, id);
        var money = await ctx.Orders.MoneyAsync(order);
        Assert.Equal(4_000m, money.Shares[0].Paid);     // виплачено з договору 40 000
        Assert.Equal(0m, money.Shares[0].Owed);
        await Assert.ThrowsAsync<CrmException>(() => ctx.Orders.UpdateShareAsync(CrmTestContext.Admin, shareId, ShareBasis.Fixed, 1m));
        await Assert.ThrowsAsync<CrmException>(() => ctx.Orders.DeleteShareAsync(CrmTestContext.Admin, shareId));
    }

    [Theory]
    [InlineData(ShareBasis.ContractPercent, -1)]
    [InlineData(ShareBasis.MaterialsPercent, 101)]
    [InlineData("bogus", 5)]
    public void Invalid_share_is_rejected(string basis, decimal value)
    {
        Assert.Throws<CrmException>(() => OrderService.ValidateShare(basis, value));
    }

    [Fact]
    public async Task Payment_validation()
    {
        var ctx = new CrmTestContext();
        var id = await ctx.Orders.CreateAsync(CrmTestContext.Admin, new NewOrderInput { ClientName = "А", Source = "brother" });

        await Assert.ThrowsAsync<CrmException>(() => ctx.Orders.AddPaymentAsync(CrmTestContext.Admin, id,
            new MoneyInput { Kind = "advance", Amount = 0m, Method = "cash", Date = new DateOnly(2026, 9, 1) }));
        await Assert.ThrowsAsync<CrmException>(() => ctx.Orders.AddPaymentAsync(CrmTestContext.Admin, id,
            new MoneyInput { Kind = "gift", Amount = 10m, Method = "cash", Date = new DateOnly(2026, 9, 1) }));
        await Assert.ThrowsAsync<CrmException>(() => ctx.Orders.AddExpenseAsync(CrmTestContext.Admin, id,
            new MoneyInput { Kind = "board", Amount = null, Date = new DateOnly(2026, 9, 1) }));
    }

    [Fact]
    public async Task Search_finds_by_phone_digits_and_name()
    {
        var ctx = new CrmTestContext();
        await ctx.Orders.CreateAsync(CrmTestContext.Admin, new NewOrderInput { ClientName = "Олена Петрівна", Phone = "067 123 45 67", Source = "brother" });
        await ctx.Orders.CreateAsync(CrmTestContext.Admin, new NewOrderInput { ClientName = "Іван", Phone = "050 999 88 77", Source = "brother" });

        Assert.Single(await ctx.Orders.ListAsync(CrmTestContext.Admin, "all", "123 45"));
        Assert.Single(await ctx.Orders.ListAsync(CrmTestContext.Admin, "all", "олена"));
        Assert.Equal(2, (await ctx.Orders.ListAsync(CrmTestContext.Admin, "open")).Count);
    }
}
