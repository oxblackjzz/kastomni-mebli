using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace KastomniMebli.Web.Crm;

/// <summary>Загальні витрати (реклама, інструмент, пальне…) — з журналом змін, як і гроші замовлень.</summary>
public sealed class CompanyExpenseService(IDbContextFactory<AppDbContext> dbs, TimeProvider time)
{
    public async Task<List<CompanyExpense>> ListAsync(CurrentUser actor, int year, int month)
    {
        Require(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1);
        return await db.CompanyExpenses.AsNoTracking()
            .Where(e => e.SpentOn >= from && e.SpentOn < to)
            .OrderByDescending(e => e.SpentOn).ThenByDescending(e => e.Id)
            .ToListAsync();
    }

    public async Task AddAsync(CurrentUser actor, MoneyInput input)
    {
        Require(actor);
        if (!Catalog.Has(Catalog.CompanyExpenseCategories, input.Kind))
            throw new CrmException("Вкажіть категорію.");
        if (input.Amount is not > 0)
            throw new CrmException("Сума має бути більшою за нуль.");
        if (input.Amount > OrderService.MaxAmount)
            throw new CrmException("Завелика сума.");

        await using var db = await dbs.CreateDbContextAsync();
        var expense = new CompanyExpense
        {
            Category = input.Kind,
            Amount = OrderFinance.Round(input.Amount.Value),
            SpentOn = input.Date,
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
            CreatedByUserId = actor.Id,
            CreatedAt = time.GetUtcNow().UtcDateTime,
        };
        db.CompanyExpenses.Add(expense);
        db.AuditLog.Add(Entry(actor, AuditActions.CompanyExpenseAdded, expense));
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(CurrentUser actor, int id)
    {
        Require(actor);
        await using var db = await dbs.CreateDbContextAsync();
        if (await db.CompanyExpenses.FirstOrDefaultAsync(e => e.Id == id) is not { } expense)
            return;
        db.CompanyExpenses.Remove(expense);
        db.AuditLog.Add(Entry(actor, AuditActions.CompanyExpenseDeleted, expense));
        await db.SaveChangesAsync();
    }

    private AuditEntry Entry(CurrentUser actor, string action, CompanyExpense e) => new()
    {
        At = time.GetUtcNow().UtcDateTime,
        UserId = actor.Id,
        UserName = actor.Name,
        Action = action,
        Amount = e.Amount,
        Details = $"{Catalog.Label(Catalog.CompanyExpenseCategories, e.Category)}, {Kyiv.Format(e.SpentOn)}" + (e.Note is null ? "" : $" — {e.Note}"),
    };

    private static void Require(CurrentUser actor)
    {
        if (!actor.CanSeeMoney)
            throw new CrmForbiddenException();
    }
}
