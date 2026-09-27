using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Data;

namespace KastomniMebli.Tests;

public class DashboardTests
{
    private static readonly string[] Materials = ["board", "edge", "hardware"];
    private static readonly List<User> People =
    [
        new User { Id = 1, DisplayName = "Конструктор" },
        new User { Id = 2, DisplayName = "Монтажник" },
    ];

    private static DateTime Utc(int month, int day, int hour = 10) => new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    private static Order MakeOrder(int id, string source, DateTime created, string status = OrderStatuses.New,
        DateTime? measured = null, DateTime? approved = null, DateTime? completed = null, decimal? contract = null) => new()
    {
        Id = id,
        Number = $"2026-{id:000}",
        Client = new Client { Name = "Клієнт " + id },
        Source = source,
        CreatedAt = created,
        Status = status,
        MeasuredAt = measured,
        ApprovedAt = approved,
        CompletedAt = completed,
        ContractAmount = contract,
    };

    private static List<Order> Sample()
    {
        // 1: з сайту, створене у вересні, завершене у вересні
        var siteDone = MakeOrder(1, "site", Utc(9, 2), OrderStatuses.Done, Utc(9, 3), Utc(9, 5), Utc(9, 25), 50_000m);
        siteDone.Payments =
        [
            new Payment { Kind = "advance", Amount = 25_000m, PaidOn = new DateOnly(2026, 9, 5) },
            new Payment { Kind = "extra", Amount = 25_000m, PaidOn = new DateOnly(2026, 9, 25) },
        ];
        siteDone.Expenses = [new Expense { Category = "board", Amount = 20_000m, SpentOn = new DateOnly(2026, 9, 6) }];
        siteDone.Shares =
        [
            new OrderShare { Id = 1, UserId = 1, Basis = ShareBasis.MaterialsPercent, Value = 5m },                   // 1 000, не виплачено
            new OrderShare { Id = 2, UserId = 2, Basis = ShareBasis.Fixed, Value = 3_000m, PaidAmount = 3_000m, PaidOn = new DateOnly(2026, 9, 26) },
        ];

        // 2: від брата, створене у вересні, лише замір
        var brotherMeasured = MakeOrder(2, "brother", Utc(9, 10), OrderStatuses.Measured, Utc(9, 12));

        // 3: з сайту, створене в серпні, в роботі; аванс у серпні, доплата у вересні
        var siteOld = MakeOrder(3, "site", Utc(8, 20), OrderStatuses.Production, Utc(8, 21), Utc(8, 25), contract: 30_000m);
        siteOld.Payments =
        [
            new Payment { Kind = "advance", Amount = 15_000m, PaidOn = new DateOnly(2026, 8, 25) },
            new Payment { Kind = "extra", Amount = 5_000m, PaidOn = new DateOnly(2026, 9, 1) },
        ];
        siteOld.Shares = [new OrderShare { Id = 3, UserId = 2, Basis = ShareBasis.ContractPercent, Value = 10m }]; // 3 000, нараховується

        // 4: з сайту, створене у вересні, скасоване
        var cancelled = MakeOrder(4, "site", Utc(9, 15), OrderStatuses.Cancelled);
        cancelled.Shares = [new OrderShare { Id = 4, UserId = 1, Basis = ShareBasis.Fixed, Value = 500m }];

        return [siteDone, brotherMeasured, siteOld, cancelled];
    }

    [Fact]
    public void Funnel_counts_orders_created_in_month()
    {
        var r = DashboardCalc.Build(Sample(), People, 2026, 9, Materials);

        Assert.Equal(3, r.Funnel.Leads);         // 1, 2, 4
        Assert.Equal(2, r.Funnel.Measured);      // 1, 2
        Assert.Equal(1, r.Funnel.Contracts);     // 1
        Assert.Equal(66.7m, r.Funnel.MeasuredRate);
        Assert.Equal(33.3m, r.Funnel.ContractRate);
    }

    [Fact]
    public void Cash_is_by_payment_date()
    {
        var r = DashboardCalc.Build(Sample(), People, 2026, 9, Materials);

        Assert.Equal(55_000m, r.Cash.Income);     // 25 000 + 25 000 + 5 000 (серпневий аванс не входить)
        Assert.Equal(20_000m, r.Cash.Expenses);
        Assert.Equal(3_000m, r.Cash.SharesPaid);
        Assert.Equal(32_000m, r.Cash.Net);
    }

    [Fact]
    public void Profit_is_for_orders_completed_in_month()
    {
        var r = DashboardCalc.Build(Sample(), People, 2026, 9, Materials);

        Assert.Equal(1, r.Completed.Count);
        Assert.Equal(50_000m, r.Completed.ContractsTotal);
        Assert.Equal(4_000m, r.Completed.Shares);
        Assert.Equal(26_000m, r.Completed.Profit);   // 50 000 − 20 000 − 4 000
    }

    [Fact]
    public void People_earned_paid_owed_and_accruing()
    {
        var r = DashboardCalc.Build(Sample(), People, 2026, 9, Materials);

        var designer = r.People.Single(p => p.UserId == 1);
        Assert.Equal(1_000m, designer.EarnedInMonth);
        Assert.Equal(0m, designer.PaidInMonth);
        Assert.Equal(1_000m, designer.Owed);         // скасоване (500) не рахується
        Assert.Equal(0m, designer.Accruing);

        var installer = r.People.Single(p => p.UserId == 2);
        Assert.Equal(3_000m, installer.EarnedInMonth);
        Assert.Equal(3_000m, installer.PaidInMonth);
        Assert.Equal(0m, installer.Owed);
        Assert.Equal(3_000m, installer.Accruing);    // 10% від 30 000 у замовленні в роботі
    }

    [Fact]
    public void Site_channel_metrics()
    {
        var r = DashboardCalc.Build(Sample(), People, 2026, 9, Materials);
        var site = r.Sources.Single(s => s.Source == "site");

        Assert.Equal(2, site.Leads);                 // 1 і 4 створені у вересні
        Assert.Equal(1, site.Contracts);
        Assert.Equal(55_000m, site.IncomeInMonth);
        Assert.Equal(26_000m, site.ProfitInMonth);
        Assert.Equal(70_000m, site.IncomeAllTime);   // 50 000 + 20 000
        Assert.Equal(0m, r.Sources.Single(s => s.Source == "brother").IncomeInMonth);
    }

    [Fact]
    public void Client_debts_list_unpaid_contracts()
    {
        var r = DashboardCalc.Build(Sample(), People, 2026, 9, Materials);

        var debt = Assert.Single(r.ClientDebts);
        Assert.Equal(3, debt.OrderId);
        Assert.Equal(10_000m, debt.Due);
    }

    [Fact]
    public void Month_boundary_uses_kyiv_time()
    {
        // 30.09 22:30 UTC = 01.10 01:30 за Києвом → жовтень
        var order = MakeOrder(1, "site", new DateTime(2026, 9, 30, 22, 30, 0, DateTimeKind.Utc));

        Assert.Equal(0, DashboardCalc.Build([order], People, 2026, 9, Materials).Funnel.Leads);
        Assert.Equal(1, DashboardCalc.Build([order], People, 2026, 10, Materials).Funnel.Leads);
    }
}
