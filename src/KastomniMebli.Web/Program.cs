using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using KastomniMebli.Web;
using KastomniMebli.Web.Components;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Leads;
using KastomniMebli.Web.Notifications;
using KastomniMebli.Web.Settings;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// Render передає порт у змінній PORT.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddRazorComponents();
// Інакше кирилиця з налаштувань потрапляє в HTML як &#x...; — працює, але нечитабельно й важче.
builder.Services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));

builder.Services.Configure<SiteSettings>(builder.Configuration.GetSection(SiteSettings.Section));
builder.Services.Configure<LeadOptions>(builder.Configuration.GetSection(LeadOptions.Section));
builder.Services.Configure<ProxyOptions>(builder.Configuration.GetSection(ProxyOptions.Section));

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var connection = DatabaseUrl.Resolve(sp.GetRequiredService<IConfiguration>())
        ?? throw new InvalidOperationException("Не задано базу даних: змінна DATABASE_URL або ConnectionStrings:Default");
    options.UseNpgsql(connection);
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<FormTiming>();
builder.Services.AddSingleton<ClientIp>();
builder.Services.AddScoped<LeadService>();

builder.Services.AddHttpClient<ILeadNotifier, TelegramNotifier>(c =>
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
    o.OnRejected = async (ctx, ct) =>
    {
        var http = ctx.HttpContext;
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
app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/healthz", () => Results.Text("ok"));
app.MapLeadEndpoints();
app.MapRazorComponents<App>();

app.Run();

public partial class Program;
