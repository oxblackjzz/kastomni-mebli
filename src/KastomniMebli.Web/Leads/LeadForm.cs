using System.Text.RegularExpressions;

namespace KastomniMebli.Web.Leads;

/// <summary>
/// Дані форми заявки так, як вони прийшли з браузера, і серверна валідація.
/// Імена полів у HTML: name, phone, types, location, dimensions, comment, website (пастка), t (час).
/// </summary>
public sealed partial class LeadForm
{
    public const int NameMax = 80;
    public const int PhoneRawMax = 40;
    public const int LocationMax = 150;
    public const int DimensionsMax = 300;
    public const int CommentMax = 2000;

    public string? Name { get; init; }
    public string? Phone { get; init; }
    public IReadOnlyList<string> Types { get; init; } = [];
    public string? Location { get; init; }
    public string? Dimensions { get; init; }
    public string? Comment { get; init; }

    /// <summary>Поле-пастка: людина його не бачить, бот заповнює.</summary>
    public string? Honeypot { get; init; }

    /// <summary>Захищена мітка часу показу форми (див. <see cref="FormTiming"/>).</summary>
    public string? TimingToken { get; init; }

    public bool IsBot => !string.IsNullOrWhiteSpace(Honeypot);

    public static LeadForm From(IFormCollection form) => new()
    {
        Name = form["name"],
        Phone = form["phone"],
        Types = form["types"].Where(t => t is not null).Select(t => t!).ToArray(),
        Location = form["location"],
        Dimensions = form["dimensions"],
        Comment = form["comment"],
        Honeypot = form["website"],
        TimingToken = form["t"],
    };

    public LeadValidationResult Validate()
    {
        var errors = new Dictionary<string, string>();

        var name = Clean(Name, multiline: false);
        if (name.Length == 0)
            errors["name"] = "Вкажіть ім'я.";
        else if (name.Length > NameMax)
            errors["name"] = $"Ім'я задовге (до {NameMax} символів).";

        var phoneRaw = (Phone ?? "").Trim();
        var phone = "";
        if (phoneRaw.Length == 0)
            errors["phone"] = "Вкажіть телефон.";
        else if (phoneRaw.Length > PhoneRawMax || !PhoneNumber.TryNormalize(phoneRaw, out phone))
            errors["phone"] = "Перевірте номер: потрібен український номер, наприклад 067 123 45 67.";

        var location = Clean(Location, multiline: false);
        CheckLength("location", location, LocationMax);
        var dimensions = Clean(Dimensions, multiline: true);
        CheckLength("dimensions", dimensions, DimensionsMax);
        var comment = Clean(Comment, multiline: true);
        CheckLength("comment", comment, CommentMax);

        // Невідомі значення (підроблений запит) просто відкидаємо.
        var types = Types.Select(t => t.Trim()).Where(FurnitureTypes.IsKnown).Distinct().ToList();

        if (errors.Count > 0)
            return new LeadValidationResult(null, errors);

        return new LeadValidationResult(new ValidLead(
            name, phone, phoneRaw, types,
            NullIfEmpty(location), NullIfEmpty(dimensions), NullIfEmpty(comment)), errors);

        void CheckLength(string field, string value, int max)
        {
            if (value.Length > max)
                errors[field] = $"Задовгий текст (до {max} символів).";
        }
    }

    private static string Clean(string? value, bool multiline)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var text = value.Replace("\r\n", "\n");
        text = ControlChars().Replace(text, "");
        text = multiline
            ? MultiNewlines().Replace(text, "\n\n")
            : Whitespace().Replace(text, " ");
        return text.Trim();
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    [GeneratedRegex(@"[\p{Cc}-[\n\t]]")]
    private static partial Regex ControlChars();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultiNewlines();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

public sealed record ValidLead(
    string Name,
    string Phone,
    string PhoneRaw,
    IReadOnlyList<string> Types,
    string? Location,
    string? Dimensions,
    string? Comment);

public sealed record LeadValidationResult(ValidLead? Lead, IReadOnlyDictionary<string, string> Errors)
{
    public bool IsValid => Lead is not null;
}
