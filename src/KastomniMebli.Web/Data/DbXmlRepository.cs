using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace KastomniMebli.Web.Data;

/// <summary>
/// Зберігає ключі DataProtection у таблиці data_protection_keys.
/// Без цього кожен редеплой (новий контейнер — нові ключі) викидав би всіх із CRM.
/// </summary>
public sealed class DbXmlRepository(IServiceScopeFactory scopes) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.DataProtectionKeys.Select(k => k.Xml).ToList().Select(XElement.Parse).ToList();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.DataProtectionKeys.Add(new DataProtectionKey
        {
            FriendlyName = friendlyName,
            Xml = element.ToString(SaveOptions.DisableFormatting),
        });
        db.SaveChanges();
    }
}
