using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Settings;
using Microsoft.Extensions.Options;

namespace KastomniMebli.Web.Posting;

public static class Networks
{
    public const string Telegram = "telegram";
    public const string Facebook = "facebook";
    public const string Instagram = "instagram";

    public static readonly IReadOnlyList<(string Key, string Label)> All =
    [
        (Telegram, "Telegram-канал"),
        (Facebook, "Facebook"),
        (Instagram, "Instagram"),
    ];

    public static string Label(string key) => Catalog.Label(All, key);
}

public sealed record PublishResult(string ExternalId, string? Url);

/// <summary>Помилка публікації з текстом для журналу.</summary>
public sealed class PublishException(string message) : Exception(message);

public interface IPostChannel
{
    string Network { get; }

    /// <summary>null — налаштовано; інакше — що задати (для підказки в CRM).</summary>
    string? ConfigProblem { get; }

    /// <summary>null — пост підходить для цієї мережі; інакше — чому ні.</summary>
    string? CheckPost(string text, int photoCount);

    Task<PublishResult> PublishAsync(Post post, IReadOnlyList<PostPhoto> photos, CancellationToken ct);
}

/// <summary>Спільне: публічна адреса фото для Meta й локальний шлях для Telegram.</summary>
public sealed class PostMedia(IOptions<SiteSettings> site, FileStore store)
{
    public const string PublicPrefix = "/media/posty/";

    public string? BaseUrl => string.IsNullOrWhiteSpace(site.Value.BaseUrl) ? null : site.Value.BaseUrl.TrimEnd('/');

    /// <summary>Meta забирає фото за URL — потрібна публічна https-адреса сайту.</summary>
    public bool HasPublicHttps => BaseUrl?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true;

    public string PublicUrl(PostPhoto photo) => $"{BaseUrl}{PublicPrefix}{photo.PublicKey}.jpg";

    public string LocalPath(PostPhoto photo) => store.FullPath(photo.Path);
}
