using System.Text.RegularExpressions;

namespace KastomniMebli.Web.Leads;

/// <summary>
/// Звідки прийшов відвідувач: мітки utm_* з посилання (перший візит, запам'ятовує браузер) або сайт-реферер.
/// Приклад посилання для реклами: https://…/?utm_source=instagram&amp;utm_campaign=kuhni_zhovten
/// </summary>
public sealed partial record Attribution(string? Source, string? Medium, string? Campaign, string? Referrer)
{
    public const int MaxLength = 100;
    public const string Direct = "direct";

    public static Attribution From(IFormCollection form) => new(
        Clean(form["utm_source"]),
        Clean(form["utm_medium"]),
        Clean(form["utm_campaign"]),
        CleanHost(form["ref"]));

    /// <summary>
    /// Канал для звітів: utm_source, інакше — розпізнаний реферер (instagram, facebook, google, telegram…),
    /// інакше — сам домен реферера, інакше — «direct» (набрали адресу, закладка, месенджер без реферера).
    /// </summary>
    public string Channel =>
        Source?.ToLowerInvariant() ?? (Referrer is null ? Direct : KnownChannel(Referrer) ?? Referrer);

    public static string? KnownChannel(string host)
    {
        var h = host.ToLowerInvariant();
        return h switch
        {
            _ when h.Contains("instagram") => "instagram",
            _ when h.Contains("facebook") || h == "fb.com" || h.EndsWith(".fb.com") => "facebook",
            _ when h.Contains("google.") => "google",
            _ when h.Contains("t.me") || h.Contains("telegram") => "telegram",
            _ when h.Contains("tiktok") => "tiktok",
            _ when h.Contains("youtube") || h == "youtu.be" => "youtube",
            _ when h.Contains("olx.") => "olx",
            _ when h.Contains("viber") => "viber",
            _ => null,
        };
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = Unsafe().Replace(value.Trim(), "");
        return v.Length == 0 ? null : v.Length > MaxLength ? v[..MaxLength] : v;
    }

    private static string? CleanHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim().ToLowerInvariant();
        if (Uri.TryCreate(v, UriKind.Absolute, out var uri))
            v = uri.Host;
        v = v.StartsWith("www.") ? v[4..] : v;
        return HostPattern().IsMatch(v) ? v : null;
    }

    [GeneratedRegex(@"[^\p{L}\p{N}_\-\. ]")]
    private static partial Regex Unsafe();

    [GeneratedRegex(@"^[a-z0-9\-\.]{3,100}$")]
    private static partial Regex HostPattern();
}
