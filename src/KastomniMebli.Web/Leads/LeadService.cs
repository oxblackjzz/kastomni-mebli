using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Notifications;

namespace KastomniMebli.Web.Leads;

public sealed class LeadService(
    AppDbContext db,
    OrderService orders,
    ILeadNotifier notifier,
    TimeProvider time,
    ILogger<LeadService> log)
{
    /// <summary>
    /// Спершу зберігаємо заявку, потім створюємо замовлення в CRM і сповіщаємо.
    /// Ні збій CRM, ні збій Telegram не губить заявку: помилки — в лог (і в telegram_error),
    /// а замовлення для «загублених» заявок створиться при наступному старті (BackfillLeadsAsync).
    /// </summary>
    public async Task<Lead> SubmitAsync(ValidLead input, string? ip, string? userAgent)
    {
        var lead = new Lead
        {
            CreatedAt = time.GetUtcNow().UtcDateTime,
            Name = input.Name,
            Phone = input.Phone,
            PhoneRaw = input.PhoneRaw,
            FurnitureTypes = input.Types.ToList(),
            Location = input.Location,
            Dimensions = input.Dimensions,
            Comment = input.Comment,
            Source = LeadSources.Site,
            Status = LeadStatuses.New,
            IpHash = ClientIp.Hash(ip),
            UserAgent = userAgent is { Length: > 512 } ? userAgent[..512] : userAgent,
        };

        // Токен запиту не передаємо: якщо клієнт закрив вкладку, заявка все одно має зберегтися.
        db.Leads.Add(lead);
        await db.SaveChangesAsync(CancellationToken.None);
        log.LogInformation("Нова заявка #{LeadId}", lead.Id);

        int? orderId = null;
        try
        {
            orderId = await orders.CreateFromLeadAsync(lead);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Заявку #{LeadId} збережено, але замовлення в CRM не створено", lead.Id);
        }

        try
        {
            await notifier.NotifyAsync(lead, orderId, CancellationToken.None);
            lead.TelegramSentAt = time.GetUtcNow().UtcDateTime;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Заявку #{LeadId} збережено, але не надіслано в Telegram", lead.Id);
            var message = $"{ex.GetType().Name}: {ex.Message}";
            lead.TelegramError = message.Length > 500 ? message[..500] : message;
        }

        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Сама заявка вже в базі — не показуємо клієнту помилку через службове поле.
            log.LogError(ex, "Не вдалося записати статус Telegram для заявки #{LeadId}", lead.Id);
        }

        return lead;
    }
}
