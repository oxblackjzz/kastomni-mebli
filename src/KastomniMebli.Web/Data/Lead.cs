namespace KastomniMebli.Web.Data;

/// <summary>
/// Заявка з сайту. На етапі 2 стає вхідною точкою CRM: із неї створюється замовлення.
/// </summary>
public class Lead
{
    public long Id { get; set; }

    /// <summary>Час надходження, UTC.</summary>
    public DateTime CreatedAt { get; set; }

    public string Name { get; set; } = "";

    /// <summary>Нормалізований номер: +380XXXXXXXXX.</summary>
    public string Phone { get; set; } = "";

    /// <summary>Номер так, як його ввів клієнт.</summary>
    public string PhoneRaw { get; set; } = "";

    /// <summary>Ключі з <see cref="Leads.FurnitureTypes"/>.</summary>
    public List<string> FurnitureTypes { get; set; } = [];

    public string? Location { get; set; }
    public string? Dimensions { get; set; }
    public string? Comment { get; set; }

    public string Source { get; set; } = LeadSources.Site;
    public string Status { get; set; } = LeadStatuses.New;

    /// <summary>Скорочений хеш IP — щоб бачити повтори зі спаму, не зберігаючи саму адресу.</summary>
    public string? IpHash { get; set; }
    public string? UserAgent { get; set; }

    // Звідки прийшов (перший візит): мітки з посилання й сайт-реферер.
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }
    public string? Referrer { get; set; }

    public DateTime? TelegramSentAt { get; set; }
    public string? TelegramError { get; set; }
}

public static class LeadSources
{
    public const string Site = "site";
}

public static class LeadStatuses
{
    public const string New = "new";
}
