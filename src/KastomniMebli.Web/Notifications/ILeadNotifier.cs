using KastomniMebli.Web.Data;

namespace KastomniMebli.Web.Notifications;

public interface ILeadNotifier
{
    /// <summary>Кидає виняток, якщо сповіщення не дійшло.</summary>
    Task NotifyAsync(Lead lead, CancellationToken ct);
}
