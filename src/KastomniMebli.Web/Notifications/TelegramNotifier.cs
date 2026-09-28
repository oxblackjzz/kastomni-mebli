using System.Text;
using System.Text.Json;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Leads;
using KastomniMebli.Web.Settings;
using Microsoft.Extensions.Options;

namespace KastomniMebli.Web.Notifications;

public interface ITelegramSender
{
    /// <summary>Загальний чат команди — TELEGRAM_CHAT_ID (кілька id через кому).</summary>
    IReadOnlyList<string> TeamChatIds { get; }

    /// <summary>Кидає виняток, якщо Telegram не налаштовано або повідомлення не дійшло.</summary>
    Task SendAsync(string chatId, string html, CancellationToken ct);
}

/// <summary>
/// Telegram Bot API (sendMessage). TELEGRAM_BOT_TOKEN — токен від @BotFather.
/// </summary>
public sealed class TelegramSender(HttpClient http, IConfiguration config) : ITelegramSender
{
    public IReadOnlyList<string> TeamChatIds =>
        (config["TELEGRAM_CHAT_ID"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public async Task SendAsync(string chatId, string html, CancellationToken ct)
    {
        var token = config["TELEGRAM_BOT_TOKEN"];
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Telegram не налаштовано: немає TELEGRAM_BOT_TOKEN");

        using var response = await http.PostAsJsonAsync($"bot{token}/sendMessage", new
        {
            chat_id = chatId,
            text = html,
            parse_mode = "HTML",
            link_preview_options = new { is_disabled = true },
        }, ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Telegram, чат {chatId}: {(int)response.StatusCode} {await ReadDescription(response, ct)}");
    }

    private static async Task<string> ReadDescription(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }
}

/// <summary>Заявка з сайту → загальний чат команди.</summary>
public sealed class TelegramNotifier(ITelegramSender telegram, IOptions<SiteSettings> site) : ILeadNotifier
{
    public async Task NotifyAsync(Lead lead, int? orderId, CancellationToken ct)
    {
        var chatIds = telegram.TeamChatIds;
        if (chatIds.Count == 0)
            throw new InvalidOperationException("Telegram не налаштовано: немає TELEGRAM_CHAT_ID");

        var text = TelegramMessage.ForLead(lead, TelegramMessage.KyivTime);
        if (orderId is not null && TelegramMessage.OrderLink(site.Value, orderId.Value) is { } link)
            text += "\n\n" + link;

        var failures = new List<string>();
        foreach (var chatId in chatIds)
        {
            try
            {
                await telegram.SendAsync(chatId, text, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                failures.Add(ex.Message);
            }
        }
        if (failures.Count > 0)
            throw new HttpRequestException(string.Join("; ", failures));
    }
}

public static class TelegramMessage
{
    public static TimeZoneInfo KyivTime => Crm.Kyiv.Zone;

    public static string ForLead(Lead lead, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(lead.CreatedAt, DateTimeKind.Utc), tz);
        var types = lead.FurnitureTypes.Count > 0
            ? string.Join(", ", lead.FurnitureTypes.Select(FurnitureTypes.Label))
            : null;

        var sb = new StringBuilder();
        sb.Append("<b>Нова заявка з сайту</b> #").Append(lead.Id).Append('\n').Append('\n');
        Line(sb, "Ім'я", lead.Name);
        Line(sb, "Телефон", PhoneNumber.Format(lead.Phone));
        Line(sb, "Що потрібно", types);
        Line(sb, "Район / місто", lead.Location);
        Line(sb, "Розміри", lead.Dimensions);
        Line(sb, "Коментар", lead.Comment);
        var channel = new Attribution(lead.UtmSource, lead.UtmMedium, lead.UtmCampaign, lead.Referrer).Channel;
        Line(sb, "Звідки", lead.UtmCampaign is null ? channel : $"{channel} · {lead.UtmCampaign}");
        Line(sb, "Час", local.ToString("dd.MM.yyyy HH:mm"));
        return sb.ToString().TrimEnd();
    }

    /// <summary>Посилання на замовлення в CRM, якщо задано Site:BaseUrl.</summary>
    public static string? OrderLink(SiteSettings site, int orderId) =>
        string.IsNullOrWhiteSpace(site.BaseUrl)
            ? null
            : $"<a href=\"{Escape(site.BaseUrl.TrimEnd('/'))}/crm/zamovlennia/{orderId}\">Відкрити в CRM</a>";

    public static void Line(StringBuilder sb, string label, string? value) =>
        sb.Append("<b>").Append(label).Append(":</b> ")
          .Append(string.IsNullOrWhiteSpace(value) ? "—" : Escape(value))
          .Append('\n');

    // Bot API у режимі HTML вимагає екранувати лише ці символи.
    public static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
