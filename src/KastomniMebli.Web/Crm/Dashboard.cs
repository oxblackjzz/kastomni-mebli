using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace KastomniMebli.Web.Crm;

public sealed record FunnelReport(int Leads, int Measured, int Contracts)
{
    public decimal MeasuredRate => Rate(Measured);
    public decimal ContractRate => Rate(Contracts);
    private decimal Rate(int n) => Leads == 0 ? 0 : Math.Round(n * 100m / Leads, 1);
}

/// <summary>Рух грошей у місяці: що реально прийшло і пішло.</summary>
public sealed record CashReport(decimal Income, decimal Expenses, decimal SharesPaid)
{
    public decimal Net => Income - Expenses - SharesPaid;
}

/// <summary>Замовлення, завершені в місяці: договори й прибуток по них.</summary>
public sealed record CompletedReport(int Count, decimal ContractsTotal, decimal Received, decimal Expenses, decimal Shares, decimal Profit);

public sealed record PersonReport(int UserId, string Name, decimal EarnedInMonth, decimal PaidInMonth, decimal Owed, decimal Accruing);

public sealed record SourceReport(string Source, string Label, int Leads, int Contracts, decimal IncomeInMonth, decimal ProfitInMonth, decimal IncomeAllTime);

public sealed record ClientDebt(int OrderId, string Number, string ClientName, decimal Contract, decimal Received, decimal Due);

/// <summary>Заявки з сайту за каналом (instagram, google, direct…) — яка реклама приводить клієнтів і гроші.</summary>
public sealed record ChannelReport(string Channel, int Leads, int Contracts, decimal IncomeInMonth, decimal IncomeAllTime);

/// <summary>Креслення для меблярів — окремо: це дохід конструктора, не команди.</summary>
public sealed record B2bReport(int NewInMonth, int OpenNow, int PaidInMonth, decimal IncomeInMonth, decimal IncomeAllTime, decimal Unpaid);

public sealed record MonthReport(
    int Year,
    int Month,
    FunnelReport Funnel,
    CashReport Cash,
    CompletedReport Completed,
    IReadOnlyList<PersonReport> People,
    IReadOnlyList<SourceReport> Sources,
    IReadOnlyList<ClientDebt> ClientDebts,
    B2bReport B2b,
    IReadOnlyList<ChannelReport> SiteChannels);

/// <summary>
/// Звіт за місяць (київський час). Правила:
/// • заявки й конверсія — за замовленнями, створеними в місяці (дійшли до заміру / до договору);
/// • виручка — оплати з датою в місяці (гроші, що реально прийшли), витрати — за датою витрати;
/// • прибуток і заробіток учасників — за замовленнями, завершеними в місяці;
/// • «винні» учасникам — невиплачені частки всіх завершених замовлень; «нараховується» — по відкритих;
/// • скасовані замовлення в частки й борги не входять;
/// • B2B-креслення рахуються окремо (B2bReport) і в цифри команди не входять.
/// </summary>
public static class DashboardCalc
{
    public static MonthReport Build(IReadOnlyList<Order> allOrders, IReadOnlyList<User> users, int year, int month,
        IReadOnlyCollection<string> materialCategories)
    {
        bool InMonth(DateTime? utc) => utc is { } d && Kyiv.ToLocal(d) is var l && l.Year == year && l.Month == month;
        bool DayInMonth(DateOnly? d) => d is { } x && x.Year == year && x.Month == month;

        var b2bOrders = allOrders.Where(o => o.Kind == OrderKinds.B2b).ToList();
        var b2b = new B2bReport(
            NewInMonth: b2bOrders.Count(o => InMonth(o.CreatedAt)),
            OpenNow: b2bOrders.Count(o => OrderStatuses.IsOpen(o.Status)),
            PaidInMonth: b2bOrders.Count(o => o.Status == OrderStatuses.Paid && InMonth(o.CompletedAt)),
            IncomeInMonth: OrderFinance.Received(b2bOrders.SelectMany(o => o.Payments).Where(p => DayInMonth(p.PaidOn))),
            IncomeAllTime: OrderFinance.Received(b2bOrders.SelectMany(o => o.Payments)),
            Unpaid: b2bOrders
                .Where(o => o.Status != OrderStatuses.Cancelled && o.ContractAmount is not null)
                .Sum(o => Math.Max(0, o.ContractAmount!.Value - OrderFinance.Received(o.Payments))));

        var orders = allOrders.Where(o => o.Kind != OrderKinds.B2b).ToList();
        var money = orders.ToDictionary(o => o.Id, o => OrderFinance.Calculate(o, materialCategories));

        var created = orders.Where(o => InMonth(o.CreatedAt)).ToList();
        var funnel = new FunnelReport(
            created.Count,
            created.Count(o => o.MeasuredAt is not null),
            created.Count(o => o.ApprovedAt is not null));

        var cash = new CashReport(
            OrderFinance.Received(orders.SelectMany(o => o.Payments).Where(p => DayInMonth(p.PaidOn))),
            orders.SelectMany(o => o.Expenses).Where(e => DayInMonth(e.SpentOn)).Sum(e => e.Amount),
            orders.SelectMany(o => o.Shares).Where(s => DayInMonth(s.PaidOn)).Sum(s => s.PaidAmount ?? 0));

        var completed = orders.Where(o => o.Status == OrderStatuses.Done && InMonth(o.CompletedAt)).ToList();
        var completedReport = new CompletedReport(
            completed.Count,
            completed.Sum(o => money[o.Id].Contract),
            completed.Sum(o => money[o.Id].Received),
            completed.Sum(o => money[o.Id].Expenses),
            completed.Sum(o => money[o.Id].SharesTotal),
            completed.Sum(o => money[o.Id].Profit));

        var shareRows = orders
            .Where(o => o.Status != OrderStatuses.Cancelled)
            .SelectMany(o => money[o.Id].Shares.Select(s => (Order: o, Share: s)))
            .ToList();
        var paidRows = orders.SelectMany(o => o.Shares).Where(s => DayInMonth(s.PaidOn)).ToList();
        var people = users
            .Select(u => new PersonReport(
                u.Id,
                u.DisplayName,
                EarnedInMonth: shareRows.Where(r => r.Share.UserId == u.Id && completed.Contains(r.Order)).Sum(r => r.Share.Amount),
                PaidInMonth: paidRows.Where(s => s.UserId == u.Id).Sum(s => s.PaidAmount ?? 0),
                Owed: shareRows.Where(r => r.Share.UserId == u.Id && r.Order.Status == OrderStatuses.Done).Sum(r => r.Share.Owed),
                Accruing: shareRows.Where(r => r.Share.UserId == u.Id && OrderStatuses.IsOpen(r.Order.Status)).Sum(r => r.Share.Owed)))
            .Where(p => p.EarnedInMonth != 0 || p.PaidInMonth != 0 || p.Owed != 0 || p.Accruing != 0)
            .ToList();

        var sources = Catalog.Sources
            .Select(s =>
            {
                var ofSource = orders.Where(o => o.Source == s.Key).ToList();
                var createdOfSource = ofSource.Where(o => InMonth(o.CreatedAt)).ToList();
                return new SourceReport(
                    s.Key,
                    s.Label,
                    createdOfSource.Count,
                    createdOfSource.Count(o => o.ApprovedAt is not null),
                    OrderFinance.Received(ofSource.SelectMany(o => o.Payments).Where(p => DayInMonth(p.PaidOn))),
                    ofSource.Where(completed.Contains).Sum(o => money[o.Id].Profit),
                    OrderFinance.Received(ofSource.SelectMany(o => o.Payments)));
            })
            .ToList();

        var debts = orders
            .Where(o => o.ApprovedAt is not null && o.Status != OrderStatuses.Cancelled && money[o.Id].BalanceDue > 0)
            .Select(o => new ClientDebt(o.Id, o.Number, o.Client?.Name ?? "", money[o.Id].Contract, money[o.Id].Received, money[o.Id].BalanceDue))
            .OrderByDescending(d => d.Due)
            .ToList();

        var siteChannels = orders
            .Where(o => o.Source == "site")
            .GroupBy(o => o.Channel ?? Leads.Attribution.Direct)
            .Select(g =>
            {
                var createdInMonth = g.Where(o => InMonth(o.CreatedAt)).ToList();
                return new ChannelReport(
                    g.Key,
                    createdInMonth.Count,
                    createdInMonth.Count(o => o.ApprovedAt is not null),
                    OrderFinance.Received(g.SelectMany(o => o.Payments).Where(p => DayInMonth(p.PaidOn))),
                    OrderFinance.Received(g.SelectMany(o => o.Payments)));
            })
            .Where(c => c.Leads > 0 || c.IncomeAllTime != 0)
            .OrderByDescending(c => c.IncomeInMonth).ThenByDescending(c => c.Leads).ThenBy(c => c.Channel)
            .ToList();

        return new MonthReport(year, month, funnel, cash, completedReport, people, sources, debts, b2b, siteChannels);
    }
}

public sealed class DashboardService(IDbContextFactory<AppDbContext> dbs, SettingsService settings)
{
    public async Task<MonthReport> BuildAsync(CurrentUser actor, int year, int month)
    {
        if (!actor.CanSeeMoney)
            throw new CrmForbiddenException();
        await using var db = await dbs.CreateDbContextAsync();
        var orders = await db.Orders.AsNoTracking().AsSplitQuery()
            .Include(o => o.Client)
            .Include(o => o.Payments)
            .Include(o => o.Expenses)
            .Include(o => o.Shares)
            .ToListAsync();
        var users = await db.Users.AsNoTracking().OrderBy(u => u.DisplayName).ToListAsync();
        return DashboardCalc.Build(orders, users, year, month, await settings.GetMaterialCategoriesAsync());
    }
}
