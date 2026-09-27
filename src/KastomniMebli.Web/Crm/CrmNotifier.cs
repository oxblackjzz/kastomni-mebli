using System.Text;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Leads;
using KastomniMebli.Web.Notifications;
using KastomniMebli.Web.Settings;
using Microsoft.Extensions.Options;

namespace KastomniMebli.Web.Crm;

/// <summary>
/// Сповіщення CRM у Telegram. Ніколи не кидає винятків: збій Telegram не має ламати роботу з замовленням.
/// </summary>
public sealed class CrmNotifier(ITelegramSender telegram, IOptions<SiteSettings> site, ILogger<CrmNotifier> log)
{
    public Task OrderCreatedAsync(Order order, string createdBy)
    {
        var sb = Header($"Нове замовлення {order.Number}", order);
        TelegramMessage.Line(sb, "Додав(ла)", createdBy);
        return SendAsync(telegram.TeamChatIds, Finish(sb, order));
    }

    /// <summary>Статус «Монтаж» — монтажнику особисто (і в загальний чат).</summary>
    public Task InstallationAsync(Order order, IEnumerable<User> installers)
    {
        var sb = Header($"Монтаж: замовлення {order.Number}", order);
        TelegramMessage.Line(sb, "Дата монтажу", Kyiv.Format(order.InstallDate));
        var personal = installers.Where(u => !string.IsNullOrWhiteSpace(u.TelegramChatId)).Select(u => u.TelegramChatId!);
        return SendAsync(personal.Concat(telegram.TeamChatIds).Distinct(), Finish(sb, order));
    }

    public Task ReminderAsync(Order order, string what, DateTime whenUtc, IEnumerable<User> people)
    {
        var sb = Header($"Нагадування: завтра {what}", order);
        TelegramMessage.Line(sb, "Коли", Kyiv.Format(whenUtc));
        var personal = people.Where(u => !string.IsNullOrWhiteSpace(u.TelegramChatId)).Select(u => u.TelegramChatId!);
        return SendAsync(personal.Concat(telegram.TeamChatIds).Distinct(), Finish(sb, order));
    }

    private static StringBuilder Header(string title, Order order)
    {
        var sb = new StringBuilder();
        sb.Append("<b>").Append(TelegramMessage.Escape(title)).Append("</b>\n\n");
        TelegramMessage.Line(sb, "Клієнт", order.Client?.Name);
        TelegramMessage.Line(sb, "Телефон", order.Client?.Phone is { } p ? PhoneNumber.Format(p) : null);
        TelegramMessage.Line(sb, "Адреса", order.Address ?? order.Client?.Address);
        TelegramMessage.Line(sb, "Що", order.FurnitureTypes.Count > 0 ? string.Join(", ", order.FurnitureTypes.Select(FurnitureTypes.Label)) : null);
        return sb;
    }

    private string Finish(StringBuilder sb, Order order)
    {
        var text = sb.ToString().TrimEnd();
        return TelegramMessage.OrderLink(site.Value, order.Id) is { } link ? text + "\n\n" + link : text;
    }

    private async Task SendAsync(IEnumerable<string> chatIds, string text)
    {
        foreach (var chatId in chatIds)
        {
            try
            {
                await telegram.SendAsync(chatId, text, CancellationToken.None);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Не вдалося надіслати сповіщення CRM у Telegram");
            }
        }
    }
}
