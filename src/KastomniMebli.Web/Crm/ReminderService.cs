using KastomniMebli.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace KastomniMebli.Web.Crm;

/// <summary>
/// Щовечора (з 18:00 за Києвом) — нагадування про завтрашні заміри й монтажі:
/// відповідальному особисто (якщо вказано chat id) і в загальний чат. Раз на день.
/// </summary>
public sealed class ReminderService(IServiceScopeFactory scopes, TimeProvider time, ILogger<ReminderService> log) : BackgroundService
{
    public const int Hour = 18;
    private const string LastRunKey = "reminders_last_run";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10), time);
        do
        {
            try
            {
                await RunOnceAsync();
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                log.LogError(ex, "Помилка під час надсилання нагадувань");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Повертає кількість надісланих нагадувань (0 — ще не час або вже надіслано сьогодні).</summary>
    public async Task<int> RunOnceAsync()
    {
        var nowLocal = Kyiv.ToLocal(time.GetUtcNow().UtcDateTime);
        if (nowLocal.Hour < Hour)
            return 0;

        using var scope = scopes.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
        var today = DateOnly.FromDateTime(nowLocal).ToString("yyyy-MM-dd");
        if (await settings.GetAsync(LastRunKey) == today)
            return 0;

        var tomorrow = nowLocal.Date.AddDays(1);
        var from = Kyiv.ToUtc(tomorrow);
        var to = Kyiv.ToUtc(tomorrow.AddDays(1));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<CrmNotifier>();
        var orders = await db.Orders.AsNoTracking()
            .Include(o => o.Client)
            .Include(o => o.Assignees).ThenInclude(a => a.User)
            .Where(o => o.Status != OrderStatuses.Done && o.Status != OrderStatuses.Cancelled)
            .Where(o => (o.MeasureDate >= from && o.MeasureDate < to) || (o.InstallDate >= from && o.InstallDate < to))
            .ToListAsync();

        var sent = 0;
        foreach (var o in orders)
        {
            if (o.MeasureDate >= from && o.MeasureDate < to)
            {
                await notifier.ReminderAsync(o, "замір", o.MeasureDate!.Value, People(o, "measurer"));
                sent++;
            }
            if (o.InstallDate >= from && o.InstallDate < to)
            {
                await notifier.ReminderAsync(o, "монтаж", o.InstallDate!.Value, People(o, "installer"));
                sent++;
            }
        }

        await settings.SetAsync(LastRunKey, today);
        if (sent > 0)
            log.LogInformation("Надіслано нагадувань: {Count}", sent);
        return sent;
    }

    private static IEnumerable<User> People(Order order, string role) =>
        order.Assignees.Where(a => a.Role == role).Select(a => a.User);
}
