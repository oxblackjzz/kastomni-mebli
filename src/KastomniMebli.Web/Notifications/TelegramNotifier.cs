using System.Text;
using System.Text.Json;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Leads;

namespace KastomniMebli.Web.Notifications;

/// <summary>
/// Надсилає заявку через Telegram Bot API (sendMessage).
/// TELEGRAM_BOT_TOKEN — токен від @BotFather.
/// TELEGRAM_CHAT_ID — id групи або кілька id через кому (наприклад, твій і брата).
/// </summary>
public sealed class TelegramNotifier(HttpClient http, IConfiguration config) : ILeadNotifier
{
    public async Task NotifyAsync(Lead lead, CancellationToken ct)
    {
        var token = config["TELEGRAM_BOT_TOKEN"];
        var chatIds = (config["TELEGRAM_CHAT_ID"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (string.IsNullOrWhiteSpace(token) || chatIds.Length == 0)
            throw new InvalidOperationException("Telegram не налаштовано: немає TELEGRAM_BOT_TOKEN або TELEGRAM_CHAT_ID");

        var text = TelegramMessage.ForLead(lead, TelegramMessage.KyivTime);
        var failures = new List<string>();

        foreach (var chatId in chatIds)
        {
            using var response = await http.PostAsJsonAsync($"bot{token}/sendMessage", new
            {
                chat_id = chatId,
                text,
                parse_mode = "HTML",
                link_preview_options = new { is_disabled = true },
            }, ct);

            if (!response.IsSuccessStatusCode)
                failures.Add($"чат {chatId}: {(int)response.StatusCode} {await ReadDescription(response, ct)}");
        }

        if (failures.Count > 0)
            throw new HttpRequestException("Telegram: " + string.Join("; ", failures));
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

public static class TelegramMessage
{
    public static TimeZoneInfo KyivTime { get; } = FindKyiv();

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
        Line(sb, "Час", local.ToString("dd.MM.yyyy HH:mm"));
        return sb.ToString().TrimEnd();
    }

    private static void Line(StringBuilder sb, string label, string? value) =>
        sb.Append("<b>").Append(label).Append(":</b> ")
          .Append(string.IsNullOrWhiteSpace(value) ? "—" : Escape(value))
          .Append('\n');

    // Bot API у режимі HTML вимагає екранувати лише ці символи.
    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static TimeZoneInfo FindKyiv()
    {
        foreach (var id in new[] { "Europe/Kyiv", "Europe/Kiev", "FLE Standard Time" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz))
                return tz;
        }
        return TimeZoneInfo.Utc;
    }
}
