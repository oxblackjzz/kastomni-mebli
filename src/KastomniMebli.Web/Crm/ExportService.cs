using System.Globalization;
using System.Text;
using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace KastomniMebli.Web.Crm;

/// <summary>
/// Вивантаження місяця в CSV для Excel: UTF-8 з BOM (щоб Excel не зламав кирилицю), роздільник «;»
/// і кома в сумах — як очікує Excel з українськими регіональними налаштуваннями.
/// </summary>
public sealed class ExportService(IDbContextFactory<AppDbContext> dbs, SettingsService settings)
{
    public static readonly string[] Kinds = ["oplaty", "vytraty", "chastky", "zamovlennia"];

    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    public async Task<byte[]> ExportAsync(CurrentUser actor, string kind, int year, int month)
    {
        if (!actor.CanSeeMoney)
            throw new CrmForbiddenException();
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1);
        await using var db = await dbs.CreateDbContextAsync();
        var rows = new List<string[]>();

        switch (kind)
        {
            case "oplaty":
                rows.Add(["Дата", "Замовлення", "Клієнт", "Вид", "Спосіб", "Сума", "Примітка"]);
                var payments = await db.Payments.AsNoTracking()
                    .Where(p => p.PaidOn >= from && p.PaidOn < to)
                    .Join(db.Orders.Include(o => o.Client), p => p.OrderId, o => o.Id, (p, o) => new { p, o })
                    .OrderBy(x => x.p.PaidOn).ToListAsync();
                rows.AddRange(payments.Select(x => new[]
                {
                    Date(x.p.PaidOn), x.o.Number, x.o.Client.Name, Catalog.Label(Catalog.PaymentKinds, x.p.Kind),
                    Catalog.Label(Catalog.PaymentMethods, x.p.Method), Num(x.p.Kind == "refund" ? -x.p.Amount : x.p.Amount), x.p.Note ?? "",
                }));
                break;

            case "vytraty":
                rows.Add(["Дата", "Замовлення", "Категорія", "Сума", "Примітка"]);
                var expenses = await db.Expenses.AsNoTracking()
                    .Where(e => e.SpentOn >= from && e.SpentOn < to)
                    .Join(db.Orders, e => e.OrderId, o => o.Id, (e, o) => new { e, o.Number })
                    .ToListAsync();
                var company = await db.CompanyExpenses.AsNoTracking().Where(e => e.SpentOn >= from && e.SpentOn < to).ToListAsync();
                rows.AddRange(expenses
                    .Select(x => (x.e.SpentOn, Row: new[] { Date(x.e.SpentOn), x.Number, Catalog.Label(Catalog.ExpenseCategories, x.e.Category), Num(x.e.Amount), x.e.Note ?? "" }))
                    .Concat(company.Select(e => (e.SpentOn, Row: new[] { Date(e.SpentOn), "загальна", Catalog.Label(Catalog.CompanyExpenseCategories, e.Category), Num(e.Amount), e.Note ?? "" })))
                    .OrderBy(x => x.SpentOn).Select(x => x.Row));
                break;

            case "chastky":
                rows.Add(["Дата виплати", "Замовлення", "Хто", "Як рахували", "Виплачено"]);
                var shares = await db.OrderShares.AsNoTracking().Include(s => s.User)
                    .Where(s => s.PaidOn >= from && s.PaidOn < to)
                    .Join(db.Orders, s => s.OrderId, o => o.Id, (s, o) => new { s, o.Number })
                    .ToListAsync();
                rows.AddRange(shares.OrderBy(x => x.s.PaidOn).Select(x => new[]
                {
                    Date(x.s.PaidOn!.Value), x.Number, x.s.User.DisplayName,
                    x.s.Basis == ShareBasis.Fixed ? "фіксовано" : $"{x.s.Value.ToString("0.##", Uk)}% {Catalog.Label(Catalog.ShareBases, x.s.Basis)}",
                    Num(x.s.PaidAmount ?? 0),
                }));
                break;

            case "zamovlennia":
                rows.Add(["№", "Вид", "Клієнт", "Телефон", "Джерело", "Канал", "Статус", "Створено", "Завершено", "Договір", "Отримано", "Витрати", "Частки", "Прибуток"]);
                var (fromUtc, toUtc) = Kyiv.MonthUtc(year, month);
                var orders = await db.Orders.AsNoTracking().AsSplitQuery()
                    .Include(o => o.Client).Include(o => o.Payments).Include(o => o.Expenses).Include(o => o.Shares)
                    .Where(o => (o.CreatedAt >= fromUtc && o.CreatedAt < toUtc) || (o.CompletedAt >= fromUtc && o.CompletedAt < toUtc))
                    .OrderBy(o => o.CreatedAt).ToListAsync();
                var materials = await settings.GetMaterialCategoriesAsync();
                rows.AddRange(orders.Select(o =>
                {
                    var m = OrderFinance.Calculate(o, materials);
                    return new[]
                    {
                        o.Number, o.Kind == OrderKinds.B2b ? "B2B" : "меблі", o.Client.Name, o.Client.Phone ?? "",
                        Catalog.Label(Catalog.Sources, o.Source), o.Channel ?? "", OrderStatuses.Label(o.Kind, o.Status),
                        Kyiv.Format(o.CreatedAt, "dd.MM.yyyy"), o.CompletedAt is null ? "" : Kyiv.Format(o.CompletedAt, "dd.MM.yyyy"),
                        Num(m.Contract), Num(m.Received), Num(m.Expenses), Num(m.SharesTotal), Num(m.Profit),
                    };
                }));
                break;

            default:
                throw new CrmException("Невідомий вид вивантаження.");
        }

        return ToCsv(rows);
    }

    public static byte[] ToCsv(IEnumerable<string[]> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
            sb.Append(string.Join(';', row.Select(Escape))).Append("\r\n");
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static string Escape(string value)
    {
        // Захист від формул: Excel виконує клітинки, що починаються з = + - @.
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' && !decimal.TryParse(value, NumberStyles.Number, Uk, out _))
            value = "'" + value;
        return value.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    private static string Num(decimal value) => value.ToString("0.00", Uk);

    private static string Date(DateOnly d) => d.ToString("dd.MM.yyyy", Uk);
}
