using System.Globalization;
using System.Text.RegularExpressions;
using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Leads;

namespace KastomniMebli.Web.B2b;

/// <summary>
/// Форма замовлення креслення від мебляра. Поля: name, phone, telegram, types, due (yyyy-MM-dd), comment,
/// files (кілька), website (пастка), t (мітка часу).
/// </summary>
public sealed partial class B2bForm
{
    public const int NameMax = 120;
    public const int CommentMax = 4000;
    public const int MaxFiles = 10;
    public const long MaxTotalBytes = 60L * 1024 * 1024;

    public string? Name { get; init; }
    public string? Phone { get; init; }
    public string? Telegram { get; init; }
    public IReadOnlyList<string> Types { get; init; } = [];
    public string? Due { get; init; }
    public string? Comment { get; init; }
    public string? Honeypot { get; init; }
    public string? TimingToken { get; init; }
    public IReadOnlyList<IFormFile> Files { get; init; } = [];

    public bool IsBot => !string.IsNullOrWhiteSpace(Honeypot);

    public static B2bForm From(IFormCollection form) => new()
    {
        Name = form["name"],
        Phone = form["phone"],
        Telegram = form["telegram"],
        Types = form["types"].Where(t => t is not null).Select(t => t!).ToArray(),
        Due = form["due"],
        Comment = form["comment"],
        Honeypot = form["website"],
        TimingToken = form["t"],
        Files = form.Files.Where(f => f.Length > 0).ToList(),
    };

    public B2bValidationResult Validate(DateOnly today, long maxFileBytes)
    {
        var errors = new Dictionary<string, string>();

        var name = Regex.Replace(Name ?? "", @"\s+", " ").Trim();
        if (name.Length == 0)
            errors["name"] = "Вкажіть назву цеху або ім'я.";
        else if (name.Length > NameMax)
            errors["name"] = $"Задовго (до {NameMax} символів).";

        var phoneRaw = (Phone ?? "").Trim();
        var phone = "";
        if (phoneRaw.Length == 0)
            errors["phone"] = "Вкажіть телефон.";
        else if (!PhoneNumber.TryNormalize(phoneRaw, out phone))
            errors["phone"] = "Перевірте номер: потрібен український номер, наприклад 067 123 45 67.";

        string? telegram = null;
        if (!string.IsNullOrWhiteSpace(Telegram))
        {
            var t = Telegram.Trim();
            if (t.StartsWith("https://t.me/", StringComparison.OrdinalIgnoreCase))
                t = t["https://t.me/".Length..];
            t = t.TrimStart('@');
            if (TelegramPattern().IsMatch(t))
                telegram = t;
            else
                errors["telegram"] = "Telegram — нік на кшталт @mebli_ceh (5–32 символи: латиниця, цифри, _).";
        }

        DateOnly? due = null;
        if (!string.IsNullOrWhiteSpace(Due))
        {
            if (!DateOnly.TryParseExact(Due.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                errors["due"] = "Невірна дата.";
            else if (d < today)
                errors["due"] = "Дата вже минула.";
            else
                due = d;
        }

        var comment = (Comment ?? "").Replace("\r\n", "\n").Trim();
        if (comment.Length > CommentMax)
            errors["comment"] = $"Задовгий коментар (до {CommentMax} символів).";

        if (Files.Count > MaxFiles)
            errors["files"] = $"Не більше {MaxFiles} файлів.";
        else if (Files.FirstOrDefault(f => !FileService.IsAllowed(f.FileName)) is { } bad)
            errors["files"] = $"«{bad.FileName}» — такий тип не приймаємо. Можна: PDF, DWG, DXF, фото, ZIP.";
        else if (Files.FirstOrDefault(f => f.Length > maxFileBytes) is { } big)
            errors["files"] = $"«{big.FileName}» завеликий — до {maxFileBytes / 1024 / 1024} МБ.";
        else if (Files.Sum(f => f.Length) > MaxTotalBytes)
            errors["files"] = $"Разом файли — до {MaxTotalBytes / 1024 / 1024} МБ. Більше — надішліть у Telegram.";

        var types = Types.Select(t => t.Trim()).Where(FurnitureTypes.IsKnown).Distinct().ToList();

        return errors.Count > 0
            ? new B2bValidationResult(null, errors)
            : new B2bValidationResult(new ValidB2b(name, phone, telegram, types, due, comment.Length == 0 ? null : comment, Files), errors);
    }

    [GeneratedRegex("^[A-Za-z0-9_]{5,32}$")]
    private static partial Regex TelegramPattern();
}

public sealed record ValidB2b(
    string Name,
    string Phone,
    string? Telegram,
    IReadOnlyList<string> Types,
    DateOnly? Due,
    string? Comment,
    IReadOnlyList<IFormFile> Files);

public sealed record B2bValidationResult(ValidB2b? Request, IReadOnlyDictionary<string, string> Errors)
{
    public bool IsValid => Request is not null;
}
