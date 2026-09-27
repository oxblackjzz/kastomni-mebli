using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace KastomniMebli.Web.Crm;

/// <summary>Налаштування CRM у базі: шаблон часток, що вважати матеріалом.</summary>
public sealed class SettingsService(IDbContextFactory<AppDbContext> dbs)
{
    private const string MaterialCategoriesKey = "material_categories";

    public async Task<IReadOnlyCollection<string>> GetMaterialCategoriesAsync()
    {
        await using var db = await dbs.CreateDbContextAsync();
        var value = await db.Settings.Where(s => s.Key == MaterialCategoriesKey).Select(s => s.Value).FirstOrDefaultAsync();
        return value is null
            ? Catalog.DefaultMaterialCategories.ToHashSet()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    }

    public async Task SetMaterialCategoriesAsync(CurrentUser actor, IEnumerable<string> categories)
    {
        RequireMoney(actor);
        var value = string.Join(",", categories.Where(c => Catalog.Has(Catalog.ExpenseCategories, c)).Distinct());
        await using var db = await dbs.CreateDbContextAsync();
        var row = await db.Settings.FirstOrDefaultAsync(s => s.Key == MaterialCategoriesKey);
        if (row is null)
            db.Settings.Add(new AppSetting { Key = MaterialCategoriesKey, Value = value });
        else
            row.Value = value;
        await db.SaveChangesAsync();
    }

    public async Task<string?> GetAsync(string key)
    {
        await using var db = await dbs.CreateDbContextAsync();
        return await db.Settings.Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync();
    }

    public async Task SetAsync(string key, string value)
    {
        await using var db = await dbs.CreateDbContextAsync();
        var row = await db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (row is null)
            db.Settings.Add(new AppSetting { Key = key, Value = value });
        else
            row.Value = value;
        await db.SaveChangesAsync();
    }

    public async Task<List<ShareTemplate>> ListTemplatesAsync(CurrentUser actor)
    {
        RequireMoney(actor);
        await using var db = await dbs.CreateDbContextAsync();
        return await db.ShareTemplates.AsNoTracking().Include(t => t.User).OrderBy(t => t.User.DisplayName).ToListAsync();
    }

    public async Task SaveTemplateAsync(CurrentUser actor, int? id, int userId, string basis, decimal value, bool active)
    {
        RequireMoney(actor);
        OrderService.ValidateShare(basis, value);
        await using var db = await dbs.CreateDbContextAsync();
        if (!await db.Users.AnyAsync(u => u.Id == userId))
            throw new CrmException("Користувача не знайдено.");
        var template = id is null
            ? db.ShareTemplates.Add(new ShareTemplate()).Entity
            : await db.ShareTemplates.FirstOrDefaultAsync(t => t.Id == id) ?? throw new CrmException("Шаблон не знайдено.");
        template.UserId = userId;
        template.Basis = basis;
        template.Value = value;
        template.IsActive = active;
        await db.SaveChangesAsync();
    }

    public async Task DeleteTemplateAsync(CurrentUser actor, int id)
    {
        RequireMoney(actor);
        await using var db = await dbs.CreateDbContextAsync();
        db.ShareTemplates.RemoveRange(db.ShareTemplates.Where(t => t.Id == id));
        await db.SaveChangesAsync();
    }

    private static void RequireMoney(CurrentUser actor)
    {
        if (!actor.CanSeeMoney)
            throw new CrmForbiddenException();
    }
}
