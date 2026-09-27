namespace KastomniMebli.Web.Leads;

public static class PhoneNumber
{
    /// <summary>
    /// Приводить український номер до +380XXXXXXXXX.
    /// Приймає: 0671234567, 380671234567, +380 (67) 123-45-67, 80671234567, 671234567.
    /// Після +380 — 9 цифр, перша з них 3–9 (мобільні 5x/6x/7x/9x, міські 3x–6x).
    /// </summary>
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        foreach (var c in trimmed)
        {
            if (!char.IsAsciiDigit(c) && c is not (' ' or '-' or '(' or ')' or '+' or '.'))
                return false;
        }
        // "+" допустимий лише на початку.
        if (trimmed.LastIndexOf('+') > 0)
            return false;

        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());
        var national = digits switch
        {
            { Length: 12 } when digits.StartsWith("380") => digits[3..],
            { Length: 11 } when digits.StartsWith("80") => digits[2..],
            { Length: 10 } when digits.StartsWith('0') => digits[1..],
            { Length: 9 } => digits,
            _ => null,
        };

        if (national is null || national[0] is < '3' or > '9')
            return false;

        normalized = "+380" + national;
        return true;
    }

    /// <summary>+380671234567 → +380 67 123 45 67 (для показу).</summary>
    public static string Format(string normalized) =>
        normalized.Length == 13 && normalized.StartsWith("+380")
            ? $"+380 {normalized[4..6]} {normalized[6..9]} {normalized[9..11]} {normalized[11..13]}"
            : normalized;
}
