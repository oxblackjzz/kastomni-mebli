using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using KastomniMebli.Web;
using KastomniMebli.Web.Components;
using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Leads;
using KastomniMebli.Web.Notifications;
using KastomniMebli.Web.Posting;
using KastomniMebli.Web.Settings;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// Render передає порт у змінній PORT.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Лендінг — статичний HTML; CRM — інтерактивні сторінки (Blazor Server).
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
// Інакше кирилиця з налаштувань потрапляє в HTML як &#x...; — працює, але нечитабельно й важче.
builder.Services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));

builder.Services.Configure<SiteSettings>(builder.Configuration.GetSection(SiteSettings.Section));
builder.Services.Configure<LeadOptions>(builder.Configuration.GetSection(LeadOptions.Section));
builder.Services.Configure<ProxyOptions>(builder.Configuration.GetSection(ProxyOptions.Section));
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection(FileStorageOptions.Section));

// Фабрика — для сторінок CRM (довгоживучі з'єднання), scoped-контекст — для звичайних запитів.
builder.Services.AddDbContextFactory<AppDbContext>((sp, options) =>
{
    var connection = DatabaseUrl.Resolve(sp.GetRequiredService<IConfiguration>())
        ?? throw new InvalidOperationException("Не задано базу даних: змінна DATABASE_URL або ConnectionStrings:Default");
    options.UseNpgsql(connection);
});
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

// Ключі шифрування cookie — у базі, щоб після редеплою не викидало з CRM.
builder.Services.AddDataProtection().SetApplicationName("KastomniMebli");
builder.Services.AddOptions<KeyManagementOptions>()
    .Configure<IServiceScopeFactory>((o, scopes) => o.XmlRepository = new DbXmlRepository(scopes));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "km_auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
        o.LoginPath = AuthEndpoints.LoginPath;
        o.AccessDeniedPath = AuthEndpoints.DeniedPath;
        o.ReturnUrlParameter = "returnUrl";
        o.Events.OnValidatePrincipal = AuthEndpoints.ValidatePrincipalAsync;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<FormTiming>();
builder.Services.AddSingleton<ClientIp>();
builder.Services.AddScoped<LeadService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddSingleton<FileStore>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<PortfolioService>();
builder.Services.AddScoped<KastomniMebli.Web.B2b.B2bNotifier>();
builder.Services.AddScoped<AdminNotifier>();

// Автопостинг: кожна мережа — окремий канал; PostPublisher публікує чергу.
builder.Services.AddScoped<PostMedia>();
builder.Services.AddScoped<PostService>();
builder.Services.AddHttpClient<TelegramChannel>(c =>
    {
        c.BaseAddress = new Uri("https://api.telegram.org/");
        c.Timeout = TimeSpan.FromSeconds(60);
    })
    .RemoveAllLoggers();
builder.Services.AddHttpClient<MetaGraph>(c =>
    {
        c.BaseAddress = new Uri("https://graph.facebook.com/");
        c.Timeout = TimeSpan.FromSeconds(60);
    })
    .RemoveAllLoggers();
builder.Services.AddScoped<FacebookChannel>();
builder.Services.AddScoped<InstagramChannel>();
builder.Services.AddScoped<IPostChannel>(sp => sp.GetRequiredService<TelegramChannel>());
builder.Services.AddScoped<IPostChannel>(sp => sp.GetRequiredService<FacebookChannel>());
builder.Services.AddScoped<IPostChannel>(sp => sp.GetRequiredService<InstagramChannel>());
builder.Services.AddHostedService<PostPublisher>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<CrmNotifier>();
builder.Services.AddScoped<ILeadNotifier, TelegramNotifier>();
builder.Services.AddHostedService<ReminderService>();

builder.Services.AddHttpClient<ITelegramSender, TelegramSender>(c =>
    {
        c.BaseAddress = new Uri("https://api.telegram.org/");
        c.Timeout = TimeSpan.FromSeconds(10);
    })
    // Стандартні логи HttpClient пишуть URL, а в ньому токен бота.
    .RemoveAllLoggers();

builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy(LeadEndpoints.RateLimitPolicy, http =>
    {
        var limit = http.RequestServices.GetRequiredService<IOptions<LeadOptions>>().Value.RateLimitPerHour;
        var ip = http.RequestServices.GetRequiredService<ClientIp>().Get(http) ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
        });
    });
    // Захист від підбору пароля: 10 спроб входу за 15 хвилин з однієї IP.
    o.AddPolicy(AuthEndpoints.LoginRateLimitPolicy, http =>
    {
        var ip = http.RequestServices.GetRequiredService<ClientIp>().Get(http) ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
        });
    });
    o.OnRejected = async (ctx, ct) =>
    {
        var http = ctx.HttpContext;
        if (http.Request.Path.StartsWithSegments(AuthEndpoints.LoginSubmitPath))
        {
            http.Response.Redirect($"{AuthEndpoints.LoginPath}?error=limit");
            return;
        }
        var site = http.RequestServices.GetRequiredService<IOptions<SiteSettings>>().Value;
        if (LeadEndpoints.WantsJson(http))
        {
            http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await http.Response.WriteAsJsonAsync(new { ok = false, message = LeadEndpoints.RateLimitedMessage(site) }, ct);
        }
        else
        {
            http.Response.Redirect("/?zayavka=limit#zayavka");
        }
    };
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();

    await scope.ServiceProvider.GetRequiredService<UserService>().EnsureAdminAsync(app.Configuration);
    await scope.ServiceProvider.GetRequiredService<OrderService>().BackfillLeadsAsync();
}

if (app.Services.GetRequiredService<IOptions<ProxyOptions>>().Value.TrustForwardedHeaders)
{
    // HTTPS закінчується на проксі Render; звідти ж дізнаємось, що запит був по https.
    var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto };
    forwarded.KnownIPNetworks.Clear();
    forwarded.KnownProxies.Clear();
    app.UseForwardedHeaders(forwarded);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/healthz", () => Results.Text("ok"));
app.MapLeadEndpoints();
app.MapSeoEndpoints();
KastomniMebli.Web.B2b.B2bEndpoints.MapB2bEndpoints(app);
app.MapAuthEndpoints();
app.MapCrmEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
