using System.Security.Claims;

namespace KastomniMebli.Web.Crm.Auth;

public static class Roles
{
    public const string Admin = "admin";
    public const string Manager = "manager";
    public const string Installer = "installer";

    /// <summary>Для [Authorize(Roles = ...)] — хто бачить гроші.</summary>
    public const string MoneyViewers = Admin + "," + Manager;

    public static readonly IReadOnlyList<(string Key, string Label)> All =
    [
        (Admin, "Адміністратор"),
        (Manager, "Менеджер"),
        (Installer, "Монтажник"),
    ];

    public static string Label(string key) => Catalog.Label(All, key);
}

public static class CrmClaims
{
    public const string SecurityStamp = "km:stamp";
}

public sealed record CurrentUser(int Id, string Name, string Role)
{
    public bool IsAdmin => Role == Roles.Admin;

    /// <summary>Гроші бачать адмін і менеджер; монтажник — ні.</summary>
    public bool CanSeeMoney => Role is Roles.Admin or Roles.Manager;

    public bool IsInstaller => Role == Roles.Installer;

    public static CurrentUser? From(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return null;
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return null;
        return new CurrentUser(id, principal.FindFirstValue(ClaimTypes.Name) ?? "", principal.FindFirstValue(ClaimTypes.Role) ?? "");
    }
}
