using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace KastomniMebli.Web.Notifications;

/// <summary>
/// Повідомлення «мені»: адміністраторам з вказаним Telegram chat id, а якщо таких немає — у загальний чат.
/// Ніколи не кидає винятків.
/// </summary>
public sealed class AdminNotifier(ITelegramSender telegram, IDbContextFactory<AppDbContext> dbs, ILogger<AdminNotifier> log)
{
    public async Task SendAsync(string html)
    {
        try
        {
            List<string> chats;
            await using (var db = await dbs.CreateDbContextAsync())
            {
                chats = await db.Users.Where(u => u.IsActive && u.Role == Roles.Admin && u.TelegramChatId != null)
                    .Select(u => u.TelegramChatId!).ToListAsync();
            }
            if (chats.Count == 0)
                chats = [.. telegram.TeamChatIds];

            foreach (var chat in chats)
                await telegram.SendAsync(chat, html, CancellationToken.None);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Не вдалося надіслати повідомлення адміністратору в Telegram");
        }
    }
}
