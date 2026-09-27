using System.Text.RegularExpressions;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Leads;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KastomniMebli.Web.Crm.Auth;

public sealed record UserInput(string Login, string DisplayName, string Role, string? Phone, string? TelegramChatId);

public sealed partial class UserService(IDbContextFactory<AppDbContext> dbs, TimeProvider time, ILogger<UserService> log)
{
    public const int MinPasswordLength = 8;

    private static readonly PasswordHasher<User> Hasher = new();

    /// <summary>Перевіряє логін і пароль. null — невірно або користувач вимкнений.</summary>
    public async Task<User?> CheckPasswordAsync(string? login, string? password)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
            return null;

        await using var db = await dbs.CreateDbContextAsync();
        var normalized = login.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Login == normalized);
        if (user is null || !user.IsActive)
            return null;

        var result = Hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
            return null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = Hasher.HashPassword(user, password);
            await db.SaveChangesAsync();
        }
        return user;
    }

    public async Task<List<User>> ListAsync(bool activeOnly = false)
    {
        await using var db = await dbs.CreateDbContextAsync();
        var q = db.Users.AsNoTracking();
        if (activeOnly)
            q = q.Where(u => u.IsActive);
        return await q.OrderBy(u => u.DisplayName).ToListAsync();
    }

    public async Task<User> CreateAsync(CurrentUser actor, UserInput input, string password)
    {
        RequireAdmin(actor);
        var errors = Validate(input);
        if (password.Length < MinPasswordLength)
            errors.Add($"Пароль — щонайменше {MinPasswordLength} символів.");
        await using var db = await dbs.CreateDbContextAsync();
        var login = input.Login.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Login == login))
            errors.Add("Такий логін уже є.");
        ThrowIfAny(errors);

        var user = new User
        {
            Login = login,
            DisplayName = input.DisplayName.Trim(),
            Role = input.Role,
            Phone = NormalizePhone(input.Phone),
            TelegramChatId = Blank(input.TelegramChatId),
            CreatedAt = time.GetUtcNow().UtcDateTime,
        };
        user.PasswordHash = Hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        log.LogInformation("Створено користувача {Login} ({Role})", user.Login, user.Role);
        return user;
    }

    public async Task UpdateAsync(CurrentUser actor, int id, UserInput input)
    {
        RequireAdmin(actor);
        var errors = Validate(input);
        await using var db = await dbs.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id) ?? throw new CrmException("Користувача не знайдено.");
        var login = input.Login.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Login == login && u.Id != id))
            errors.Add("Такий логін уже є.");
        if (user.Role == Roles.Admin && input.Role != Roles.Admin && await IsLastActiveAdmin(db, user.Id))
            errors.Add("Це єдиний адміністратор — роль змінити не можна.");
        ThrowIfAny(errors);

        if (user.Role != input.Role || user.Login != login)
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.Login = login;
        user.DisplayName = input.DisplayName.Trim();
        user.Role = input.Role;
        user.Phone = NormalizePhone(input.Phone);
        user.TelegramChatId = Blank(input.TelegramChatId);
        await db.SaveChangesAsync();
    }

    public async Task SetPasswordAsync(CurrentUser actor, int id, string password)
    {
        if (!actor.IsAdmin && actor.Id != id)
            throw new CrmForbiddenException();
        if (password.Length < MinPasswordLength)
            throw new CrmException($"Пароль — щонайменше {MinPasswordLength} символів.");
        await using var db = await dbs.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id) ?? throw new CrmException("Користувача не знайдено.");
        user.PasswordHash = Hasher.HashPassword(user, password);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
    }

    public async Task SetActiveAsync(CurrentUser actor, int id, bool active)
    {
        RequireAdmin(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id) ?? throw new CrmException("Користувача не знайдено.");
        if (!active && user.Role == Roles.Admin && await IsLastActiveAdmin(db, user.Id))
            throw new CrmException("Це єдиний адміністратор — вимкнути не можна.");
        user.IsActive = active;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Перший запуск: якщо користувачів немає — створює адміністратора з ADMIN_LOGIN / ADMIN_PASSWORD.
    /// Якщо користувачі вже є — нічого не робить (змінні можна прибрати).
    /// </summary>
    public async Task EnsureAdminAsync(IConfiguration config)
    {
        await using var db = await dbs.CreateDbContextAsync();
        if (await db.Users.AnyAsync())
            return;

        var login = config["ADMIN_LOGIN"];
        var password = config["ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
        {
            log.LogWarning("У CRM немає жодного користувача. Задайте ADMIN_LOGIN і ADMIN_PASSWORD і перезапустіть сайт.");
            return;
        }
        if (password.Length < MinPasswordLength || !LoginPattern().IsMatch(login.Trim().ToLowerInvariant()))
        {
            log.LogError("ADMIN_LOGIN або ADMIN_PASSWORD не підходять (логін: латиниця/цифри, пароль від {Min} символів)", MinPasswordLength);
            return;
        }

        var user = new User
        {
            Login = login.Trim().ToLowerInvariant(),
            DisplayName = config["ADMIN_NAME"] is { Length: > 0 } name ? name : "Адміністратор",
            Role = Roles.Admin,
            CreatedAt = time.GetUtcNow().UtcDateTime,
        };
        user.PasswordHash = Hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        log.LogInformation("Створено першого адміністратора {Login}", user.Login);
    }

    private static List<string> Validate(UserInput input)
    {
        var errors = new List<string>();
        if (!LoginPattern().IsMatch(input.Login.Trim().ToLowerInvariant()))
            errors.Add("Логін: 3–40 символів, латиниця, цифри, крапка, дефіс.");
        if (string.IsNullOrWhiteSpace(input.DisplayName) || input.DisplayName.Trim().Length > 80)
            errors.Add("Вкажіть ім'я (до 80 символів).");
        if (!Catalog.Has(Roles.All, input.Role))
            errors.Add("Невідома роль.");
        if (!string.IsNullOrWhiteSpace(input.Phone) && !PhoneNumber.TryNormalize(input.Phone, out _))
            errors.Add("Невірний телефон.");
        if (!string.IsNullOrWhiteSpace(input.TelegramChatId) && !ChatIdPattern().IsMatch(input.TelegramChatId.Trim()))
            errors.Add("Telegram chat id — це число, напр. 123456789.");
        return errors;
    }

    private static Task<bool> IsLastActiveAdmin(AppDbContext db, int userId) =>
        db.Users.AllAsync(u => u.Id == userId || u.Role != Roles.Admin || !u.IsActive);

    private static void RequireAdmin(CurrentUser actor)
    {
        if (!actor.IsAdmin)
            throw new CrmForbiddenException();
    }

    private static void ThrowIfAny(List<string> errors)
    {
        if (errors.Count > 0)
            throw new CrmException(string.Join(" ", errors));
    }

    private static string? NormalizePhone(string? phone) =>
        PhoneNumber.TryNormalize(phone, out var normalized) ? normalized : null;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[a-z0-9._-]{3,40}$")]
    private static partial Regex LoginPattern();

    [GeneratedRegex("^-?[0-9]{3,20}$")]
    private static partial Regex ChatIdPattern();
}
