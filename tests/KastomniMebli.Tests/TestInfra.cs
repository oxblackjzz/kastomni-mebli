using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Leads;
using KastomniMebli.Web.Notifications;
using KastomniMebli.Web.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KastomniMebli.Tests;

/// <summary>Сайт цілком, але з базою в пам'яті й підміненим Telegram.</summary>
public sealed class SiteFactory : WebApplicationFactory<Program>
{
    public FakeNotifier Notifier { get; } = new();
    public FakeTelegram Telegram { get; } = new();
    public int RateLimitPerHour { get; init; } = 1000;
    public string FilesRoot { get; } = Path.Combine(Path.GetTempPath(), "km-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _dbName = "site-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<IDbContextFactory<AppDbContext>>();
            services.AddDbContextFactory<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));

            services.RemoveAll<ILeadNotifier>();
            services.AddSingleton<ILeadNotifier>(Notifier);
            services.RemoveAll<ITelegramSender>();
            services.AddSingleton<ITelegramSender>(Telegram);

            // Нагадування за розкладом у тестах не потрібні.
            services.RemoveAll<IHostedService>();

            services.Configure<LeadOptions>(o => o.RateLimitPerHour = RateLimitPerHour);
            services.Configure<FileStorageOptions>(o => o.Root = FilesRoot);
        });
    }

    public List<Lead> Leads()
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Leads.AsNoTracking().ToList();
    }

    public List<Order> Orders()
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.AsNoTracking().Include(o => o.Client).ToList();
    }

    public async Task<User> AddUserAsync(string login, string password, string role, string name = "Тест")
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserService>();
        var admin = new CurrentUser(0, "setup", Roles.Admin);
        return await users.CreateAsync(admin, new UserInput(login, name, role, null, null), password);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(FilesRoot))
            Directory.Delete(FilesRoot, recursive: true);
    }
}

public sealed class FakeNotifier : ILeadNotifier
{
    public List<(Lead Lead, int? OrderId)> Sent { get; } = [];
    public bool Fail { get; set; }

    public Task NotifyAsync(Lead lead, int? orderId, CancellationToken ct)
    {
        if (Fail)
            throw new HttpRequestException("Telegram недоступний");
        Sent.Add((lead, orderId));
        return Task.CompletedTask;
    }
}

public sealed class FakeTelegram : ITelegramSender
{
    public List<(string ChatId, string Text)> Sent { get; } = [];
    public bool Fail { get; set; }
    public IReadOnlyList<string> TeamChatIds { get; set; } = ["team"];

    public Task SendAsync(string chatId, string html, CancellationToken ct)
    {
        if (Fail)
            throw new HttpRequestException("Telegram недоступний");
        Sent.Add((chatId, html));
        return Task.CompletedTask;
    }
}

public sealed class TestTime(DateTime utcNow) : TimeProvider
{
    public DateTime UtcNow { get; set; } = utcNow;
    public override DateTimeOffset GetUtcNow() => new(UtcNow, TimeSpan.Zero);
}

public sealed class TestDbFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options =
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase("crm-" + Guid.NewGuid()).Options;

    public AppDbContext CreateDbContext() => new(_options);
}

/// <summary>Сервіси CRM поверх бази в пам'яті — для тестів без HTTP.</summary>
public sealed class CrmTestContext
{
    public TestDbFactory Db { get; } = new();
    public TestTime Time { get; } = new(new DateTime(2026, 9, 15, 9, 0, 0, DateTimeKind.Utc));
    public FakeTelegram Telegram { get; } = new();
    public SettingsService Settings { get; }
    public OrderService Orders { get; }
    public UserService Users { get; }

    public static readonly CurrentUser Admin = new(1, "Адмін", Roles.Admin);

    public CrmTestContext()
    {
        Settings = new SettingsService(Db);
        var notifier = new CrmNotifier(Telegram, Options.Create(new SiteSettings()), NullLogger<CrmNotifier>.Instance);
        Orders = new OrderService(Db, Settings, notifier, Time, NullLogger<OrderService>.Instance);
        Users = new UserService(Db, Time, NullLogger<UserService>.Instance);
    }

    public async Task<CurrentUser> UserAsync(string login, string role, string name)
    {
        var u = await Users.CreateAsync(Admin, new UserInput(login, name, role, null, null), "password123");
        return new CurrentUser(u.Id, u.DisplayName, u.Role);
    }
}
