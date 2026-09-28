using System.Text.Json;
using System.Text.RegularExpressions;
using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KastomniMebli.Web.Settings;

/// <summary>
/// Що можна змінити в CRM (Налаштування → Сайт). null — береться типове значення з appsettings / змінних Render.
/// </summary>
public sealed class SiteContent
{
    public string? Phone { get; set; }
    public string? Telegram { get; set; }
    public List<string>? Areas { get; set; }
    public List<TeamRole>? Team { get; set; }
    public List<FaqItem>? Faq { get; set; }
}

/// <summary>
/// IOptions&lt;SiteSettings&gt; для всього сайту: типові значення з конфігурації + зміни з CRM (таблиця settings).
/// Зміни діють одразу, без перезапуску. Завантажується при старті, оновлюється при збереженні.
/// </summary>
public sealed class SiteSettingsAccessor(IOptionsMonitor<SiteSettings> config) : IOptions<SiteSettings>
{
    public const string Key = "site_content";

    private volatile SiteContent _overrides = new();

    public SiteContent Overrides => _overrides;

    public SiteSettings Defaults => config.CurrentValue;

    public SiteSettings Value
    {
        get
        {
            var d = config.CurrentValue;
            var o = _overrides;
            return new SiteSettings
            {
                Brand = d.Brand,
                BaseUrl = d.BaseUrl,
                Phone = o.Phone ?? d.Phone,
                Telegram = o.Telegram ?? d.Telegram,
                Areas = o.Areas ?? d.Areas,
                Team = o.Team ?? d.Team,
                Faq = o.Faq ?? d.Faq,
            };
        }
    }

    public void Set(SiteContent overrides) => _overrides = overrides;

    public async Task LoadAsync(AppDbContext db)
    {
        var json = await db.Settings.Where(s => s.Key == Key).Select(s => s.Value).FirstOrDefaultAsync();
        if (json is null)
            return;
        try
        {
            _overrides = JsonSerializer.Deserialize<SiteContent>(json) ?? new SiteContent();
        }
        catch (JsonException)
        {
            _overrides = new SiteContent();
        }
    }
}

public sealed partial class SiteContentService(SiteSettingsAccessor accessor, SettingsService settings)
{
    public const int FaqMax = 20;

    public SiteContent Current => accessor.Overrides;
    public SiteSettings Effective => accessor.Value;
    public SiteSettings Defaults => accessor.Defaults;

    public async Task SaveAsync(CurrentUser actor, SiteContent content)
    {
        if (!actor.CanSeeMoney)
            throw new CrmForbiddenException();

        var clean = new SiteContent
        {
            Phone = Blank(content.Phone),
            Telegram = Blank(content.Telegram)?.TrimStart('@'),
            Areas = content.Areas?.Select(a => a.Trim()).Where(a => a.Length > 0).Distinct().ToList(),
            Team = content.Team?.Where(t => !string.IsNullOrWhiteSpace(t.Role))
                .Select(t => new TeamRole { Role = t.Role.Trim(), Description = t.Description?.Trim() ?? "", Name = Blank(t.Name) }).ToList(),
            Faq = content.Faq?.Where(f => !string.IsNullOrWhiteSpace(f.Question) && !string.IsNullOrWhiteSpace(f.Answer))
                .Select(f => new FaqItem { Question = f.Question.Trim(), Answer = f.Answer.Trim() }).ToList(),
        };

        var errors = new List<string>();
        if (clean.Phone is not null && !PhoneNumber.TryNormalize(clean.Phone, out _))
            errors.Add("Невірний телефон.");
        if (clean.Telegram is not null && !TelegramPattern().IsMatch(clean.Telegram))
            errors.Add("Telegram — нік латиницею (5–32 символи).");
        if (clean.Areas is { Count: > 10 } || clean.Areas?.Any(a => a.Length > 60) == true)
            errors.Add("Районів — до 10, кожен до 60 символів.");
        if (clean.Team?.Any(t => t.Role.Length > 60 || t.Description.Length > 300) == true)
            errors.Add("Роль — до 60 символів, опис — до 300.");
        if (clean.Faq is { Count: > FaqMax } || clean.Faq?.Any(f => f.Question.Length > 200 || f.Answer.Length > 1500) == true)
            errors.Add($"Питань — до {FaqMax}; питання до 200 символів, відповідь до 1500.");
        if (errors.Count > 0)
            throw new CrmException(string.Join(" ", errors));

        await settings.SetAsync(SiteSettingsAccessor.Key, JsonSerializer.Serialize(clean, SettingsService.Json));
        accessor.Set(clean);
    }

    /// <summary>Скинути все до типових значень з конфігурації.</summary>
    public async Task ResetAsync(CurrentUser actor)
    {
        if (!actor.CanSeeMoney)
            throw new CrmForbiddenException();
        await settings.SetAsync(SiteSettingsAccessor.Key, JsonSerializer.Serialize(new SiteContent()));
        accessor.Set(new SiteContent());
    }

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    [GeneratedRegex("^[A-Za-z0-9_]{5,32}$")]
    private static partial Regex TelegramPattern();
}
