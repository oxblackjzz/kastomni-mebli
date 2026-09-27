using KastomniMebli.Web.Leads;

namespace KastomniMebli.Web.Settings;

/// <summary>
/// Контакти й тексти сайту — секція "Site" в appsettings.json.
/// Будь-яке поле можна перевизначити змінною оточення, напр. Site__Phone.
/// На етапі 2 переїде в БД з редагуванням в адмінці.
/// </summary>
public sealed class SiteSettings
{
    public const string Section = "Site";

    public string Brand { get; set; } = "Кастомні Меблі";

    /// <summary>Публічна адреса сайту, напр. https://kastomni-mebli.onrender.com — для посилань у Telegram.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Де працюємо, напр. ["Звягель та район", "Житомир", "Рівне"].</summary>
    public List<string> Areas { get; set; } = [];

    /// <summary>Телефон для показу, напр. "+380 67 123 45 67". Порожньо — блок телефону не показуємо.</summary>
    public string Phone { get; set; } = "";

    /// <summary>Telegram без @ (особистий або бот). Порожньо — не показуємо.</summary>
    public string Telegram { get; set; } = "";

    public List<TeamRole> Team { get; set; } = [];
    public List<FaqItem> Faq { get; set; } = [];

    public string? PhoneHref =>
        PhoneNumber.TryNormalize(Phone, out var normalized) ? "tel:" + normalized : null;

    public string? TelegramHref =>
        string.IsNullOrWhiteSpace(Telegram) ? null : "https://t.me/" + Telegram.Trim().TrimStart('@');

    public string AreasText => string.Join(" · ", Areas);
}

public sealed class TeamRole
{
    public string Role { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Необов'язково. Зараз імена не показуємо.</summary>
    public string? Name { get; set; }
}

public sealed class FaqItem
{
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
}
