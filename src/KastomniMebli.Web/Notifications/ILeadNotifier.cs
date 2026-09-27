using KastomniMebli.Web.Data;

namespace KastomniMebli.Web.Notifications;

public interface ILeadNotifier
{
    /// <summary>Кидає виняток, якщо сповіщення не дійшло. orderId — для посилання на CRM.</summary>
    Task NotifyAsync(Lead lead, int? orderId, CancellationToken ct);
}
