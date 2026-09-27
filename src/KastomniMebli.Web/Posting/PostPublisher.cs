using System.Text;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Notifications;
using Microsoft.EntityFrameworkCore;

namespace KastomniMebli.Web.Posting;

/// <summary>
/// Раз на 30 секунд публікує пости, чий час настав. Кожна мережа — окремо: збій в одній не зупиняє інші;
/// помилка — у журнал (post_targets.error) і адміністратору в Telegram.
/// </summary>
public sealed class PostPublisher(IServiceScopeFactory scopes, TimeProvider time, ILogger<PostPublisher> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverInterruptedAsync();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), time);
        do
        {
            try
            {
                await PublishDueAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                log.LogError(ex, "Помилка черги публікацій");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Публікація, перервана перезапуском сервера: невідомо, чи дійшла — позначаємо помилкою, щоб не задублювати.</summary>
    public async Task RecoverInterruptedAsync()
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stuck = await db.PostTargets.Where(t => t.Status == PostTargetStatuses.Publishing).ToListAsync();
        foreach (var t in stuck)
        {
            t.Status = PostTargetStatuses.Failed;
            t.Error = "Публікацію перервав перезапуск сервера. Перевірте мережу вручну; якщо поста немає — натисніть «Повторити».";
        }
        if (stuck.Count > 0)
            await db.SaveChangesAsync();
    }

    public async Task<int> PublishDueAsync(CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var channels = scope.ServiceProvider.GetServices<IPostChannel>().ToList();
        var admin = scope.ServiceProvider.GetRequiredService<AdminNotifier>();
        var now = time.GetUtcNow().UtcDateTime;

        var targets = await db.PostTargets
            .Where(t => t.Status == PostTargetStatuses.Pending)
            .Where(t => db.Posts.Any(p => p.Id == t.PostId && !p.IsDraft && (p.ScheduledAt == null || p.ScheduledAt <= now)))
            .OrderBy(t => t.Id)
            .ToListAsync(ct);

        foreach (var target in targets)
        {
            var post = await db.Posts.AsNoTracking().FirstAsync(p => p.Id == target.PostId, ct);
            var photos = await db.PostPhotos.AsNoTracking().Where(p => p.PostId == post.Id)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync(ct);

            target.Status = PostTargetStatuses.Publishing;
            target.Attempts++;
            target.LastAttemptAt = time.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);

            try
            {
                var channel = channels.FirstOrDefault(c => c.Network == target.Network)
                    ?? throw new PublishException("Ця мережа ще не підключена.");
                var result = await channel.PublishAsync(post, photos, ct);
                target.Status = PostTargetStatuses.Done;
                target.ExternalId = result.ExternalId;
                target.ExternalUrl = result.Url;
                target.PublishedAt = time.GetUtcNow().UtcDateTime;
                target.Error = null;
                log.LogInformation("Пост #{PostId} опубліковано: {Network}", post.Id, target.Network);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                var message = ex is PublishException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}";
                target.Status = PostTargetStatuses.Failed;
                target.Error = message.Length > 1000 ? message[..1000] : message;
                log.LogWarning(ex, "Пост #{PostId}: не вдалося опублікувати в {Network}", post.Id, target.Network);
                await admin.SendAsync(FailureText(post, target));
            }
            await db.SaveChangesAsync(CancellationToken.None);
        }
        return targets.Count;
    }

    private static string FailureText(Post post, PostTarget target)
    {
        var sb = new StringBuilder();
        sb.Append("<b>Пост #").Append(post.Id).Append(": не опубліковано в ").Append(Networks.Label(target.Network)).Append("</b>\n\n");
        sb.Append(TelegramMessage.Escape(target.Error ?? "")).Append('\n');
        var preview = post.Text.Length > 120 ? post.Text[..120] + "…" : post.Text;
        if (preview.Length > 0)
            sb.Append('\n').Append("<i>").Append(TelegramMessage.Escape(preview)).Append("</i>");
        return sb.ToString();
    }
}
