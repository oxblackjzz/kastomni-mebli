using System.Security.Claims;
using KastomniMebli.Web.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace KastomniMebli.Web.Crm.Auth;

public static class AuthEndpoints
{
    public const string LoginPath = "/crm/vhid";

    /// <summary>Куди надсилається форма входу (окремо від сторінки — сторінка Blazor теж приймає POST).</summary>
    public const string LoginSubmitPath = "/crm/uviyty";
    public const string LogoutPath = "/crm/vyhid";
    public const string DeniedPath = "/crm/zaboroneno";
    public const string LoginRateLimitPolicy = "login";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Форму читаємо самі й перевіряємо антифорджері вручну: протермінований токен (сторінку відкрили
        // до редеплою) — це не помилка 500, а просто «увійдіть ще раз».
        app.MapPost(LoginSubmitPath, LoginAsync).RequireRateLimiting(LoginRateLimitPolicy);
        app.MapPost(LogoutPath, async (HttpContext http, IAntiforgery antiforgery) =>
        {
            if (await antiforgery.IsRequestValidAsync(http))
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect(LoginPath);
        });
    }

    private static async Task<IResult> LoginAsync(HttpContext http, IAntiforgery antiforgery, UserService users, ILoggerFactory loggers)
    {
        if (!http.Request.HasFormContentType || !await antiforgery.IsRequestValidAsync(http))
            return Results.Redirect($"{LoginPath}?error=expired");

        var form = await http.Request.ReadFormAsync();
        var returnUrl = SafeReturnUrl(form["returnUrl"]);
        var user = await users.CheckPasswordAsync(form["login"], form["password"]);
        if (user is null)
        {
            loggers.CreateLogger(typeof(AuthEndpoints)).LogWarning("Невдала спроба входу в CRM: {Login}", form["login"].ToString());
            return Results.Redirect($"{LoginPath}?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, CreatePrincipal(user),
            new AuthenticationProperties { IsPersistent = true });
        return Results.Redirect(returnUrl);
    }

    public static ClaimsPrincipal CreatePrincipal(User user) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(CrmClaims.SecurityStamp, user.SecurityStamp),
        ], CookieAuthenticationDefaults.AuthenticationScheme));

    /// <summary>Повертаємо лише на сторінки CRM — щоб посилання на вхід не можна було використати для редиректу на чужий сайт.</summary>
    public static string SafeReturnUrl(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith("/crm", StringComparison.Ordinal) && !url.StartsWith("//") && !url.Contains('\\')
            ? url
            : "/crm";

    /// <summary>
    /// На кожен запит звіряємо cookie з базою: вимкнений користувач, зміна пароля чи ролі — вихід.
    /// </summary>
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var current = CurrentUser.From(context.Principal);
        var stamp = context.Principal?.FindFirstValue(CrmClaims.SecurityStamp);
        if (current is not null && stamp is not null)
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var ok = await db.Users.AsNoTracking()
                .AnyAsync(u => u.Id == current.Id && u.IsActive && u.SecurityStamp == stamp && u.Role == current.Role);
            if (ok)
                return;
        }
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
