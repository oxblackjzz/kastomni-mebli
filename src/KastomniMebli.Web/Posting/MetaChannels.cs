using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using KastomniMebli.Web.Data;

namespace KastomniMebli.Web.Posting;

/// <summary>
/// Meta Graph API (Facebook-сторінка + Instagram, прив'язаний до сторінки).
/// FB_PAGE_ID, FB_PAGE_ACCESS_TOKEN (безстроковий токен сторінки), IG_USER_ID, META_GRAPH_VERSION (типово v26.0).
/// Токен передаємо в тілі запиту, а не в адресі, — щоб він не потрапляв у логи.
/// </summary>
public sealed class MetaGraph(HttpClient http, IConfiguration config)
{
    public string Version => config["META_GRAPH_VERSION"] is { Length: > 0 } v ? v : "v26.0";
    public string? Token => config["FB_PAGE_ACCESS_TOKEN"];
    public string? PageId => config["FB_PAGE_ID"];
    public string? InstagramUserId => config["IG_USER_ID"];

    /// <summary>Скільки чекати між перевірками статусу контейнера Instagram (у тестах — нуль).</summary>
    public TimeSpan PollDelay { get; set; } = TimeSpan.FromSeconds(3);

    public async Task<JsonNode> PostAsync(string path, IEnumerable<KeyValuePair<string, string>> fields, CancellationToken ct)
    {
        var all = fields.Append(new("access_token", Token ?? ""));
        using var response = await http.PostAsync($"{Version}/{path}", new FormUrlEncodedContent(all), ct);
        return await ReadAsync(response, ct);
    }

    public async Task<JsonNode> GetAsync(string path, string fields, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Version}/{path}?fields={Uri.EscapeDataString(fields)}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
        using var response = await http.SendAsync(request, ct);
        return await ReadAsync(response, ct);
    }

    private static async Task<JsonNode> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        JsonNode? json;
        try
        {
            json = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            throw new PublishException($"Meta {(int)response.StatusCode}: незрозуміла відповідь");
        }
        if (response.IsSuccessStatusCode && json is not null && json["error"] is null)
            return json;
        var message = json?["error"]?["message"]?.GetValue<string>() ?? response.ReasonPhrase;
        throw new PublishException($"Meta: {message}");
    }
}

/// <summary>
/// Facebook-сторінка: текст → /feed; одне фото → /photos; кілька → фото без публікації + /feed з attached_media.
/// </summary>
public sealed class FacebookChannel(MetaGraph meta, PostMedia media) : IPostChannel
{
    public string Network => Networks.Facebook;

    public string? ConfigProblem =>
        string.IsNullOrWhiteSpace(meta.PageId) ? "немає FB_PAGE_ID"
        : string.IsNullOrWhiteSpace(meta.Token) ? "немає FB_PAGE_ACCESS_TOKEN"
        : null;

    public string? CheckPost(string text, int photoCount) =>
        text.Length == 0 && photoCount == 0 ? "порожній пост"
        : photoCount > 10 ? "не більше 10 фото"
        : photoCount > 0 && !media.HasPublicHttps ? "для фото потрібна публічна адреса сайту (Site__BaseUrl з https)"
        : null;

    public async Task<PublishResult> PublishAsync(Post post, IReadOnlyList<PostPhoto> photos, CancellationToken ct)
    {
        if (ConfigProblem is { } problem)
            throw new PublishException("Facebook не налаштовано: " + problem);
        var text = post.Text.Trim();
        string postId;

        if (photos.Count == 0)
        {
            postId = (await meta.PostAsync($"{meta.PageId}/feed", [new("message", text)], ct))["id"]!.GetValue<string>();
        }
        else if (photos.Count == 1)
        {
            var r = await meta.PostAsync($"{meta.PageId}/photos",
                [new("url", media.PublicUrl(photos[0])), new("caption", text)], ct);
            postId = (r["post_id"] ?? r["id"])!.GetValue<string>();
        }
        else
        {
            var fields = new List<KeyValuePair<string, string>> { new("message", text) };
            for (var i = 0; i < photos.Count; i++)
            {
                var uploaded = await meta.PostAsync($"{meta.PageId}/photos",
                    [new("url", media.PublicUrl(photos[i])), new("published", "false")], ct);
                fields.Add(new($"attached_media[{i}]", new JsonObject { ["media_fbid"] = uploaded["id"]!.GetValue<string>() }.ToJsonString()));
            }
            postId = (await meta.PostAsync($"{meta.PageId}/feed", fields, ct))["id"]!.GetValue<string>();
        }

        return new PublishResult(postId, $"https://www.facebook.com/{postId}");
    }
}

/// <summary>
/// Instagram (Business/Creator, прив'язаний до сторінки): контейнер(и) → очікування FINISHED → media_publish.
/// Лише з фото (1 або карусель до 10), підпис до 2200 символів, до 30 хештегів. Фото — JPEG за публічною https-адресою.
/// </summary>
public sealed partial class InstagramChannel(MetaGraph meta, PostMedia media) : IPostChannel
{
    public const int CaptionMax = 2200;
    public const int HashtagMax = 30;

    public string Network => Networks.Instagram;

    public string? ConfigProblem =>
        string.IsNullOrWhiteSpace(meta.InstagramUserId) ? "немає IG_USER_ID"
        : string.IsNullOrWhiteSpace(meta.Token) ? "немає FB_PAGE_ACCESS_TOKEN"
        : null;

    public string? CheckPost(string text, int photoCount) =>
        photoCount == 0 ? "Instagram не публікує пост без фото"
        : photoCount > 10 ? "не більше 10 фото"
        : text.Length > CaptionMax ? $"текст довший за {CaptionMax} символів"
        : Hashtag().Count(text) > HashtagMax ? $"більше {HashtagMax} хештегів"
        : !media.HasPublicHttps ? "потрібна публічна адреса сайту (Site__BaseUrl з https)"
        : null;

    public async Task<PublishResult> PublishAsync(Post post, IReadOnlyList<PostPhoto> photos, CancellationToken ct)
    {
        if (ConfigProblem is { } problem)
            throw new PublishException("Instagram не налаштовано: " + problem);
        if (CheckPost(post.Text.Trim(), photos.Count) is { } bad)
            throw new PublishException("Instagram: " + bad);

        var ig = meta.InstagramUserId;
        var caption = post.Text.Trim();
        string containerId;

        if (photos.Count == 1)
        {
            containerId = await CreateAsync([new("image_url", media.PublicUrl(photos[0])), new("caption", caption)], ct);
        }
        else
        {
            var children = new List<string>();
            foreach (var photo in photos)
                children.Add(await CreateAsync([new("image_url", media.PublicUrl(photo)), new("is_carousel_item", "true")], ct));
            containerId = await CreateAsync(
                [new("media_type", "CAROUSEL"), new("children", string.Join(",", children)), new("caption", caption)], ct);
        }

        await WaitFinishedAsync(containerId, ct);
        var mediaId = (await meta.PostAsync($"{ig}/media_publish", [new("creation_id", containerId)], ct))["id"]!.GetValue<string>();

        string? url = null;
        try
        {
            url = (await meta.GetAsync(mediaId, "permalink", ct))["permalink"]?.GetValue<string>();
        }
        catch (PublishException)
        {
            // Посилання — приємний бонус; пост уже опубліковано.
        }
        return new PublishResult(mediaId, url);

        async Task<string> CreateAsync(KeyValuePair<string, string>[] fields, CancellationToken token) =>
            (await meta.PostAsync($"{ig}/media", fields, token))["id"]!.GetValue<string>();
    }

    /// <summary>Фото зазвичай готові одразу; чекаємо до ~1 хвилини.</summary>
    private async Task WaitFinishedAsync(string containerId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var status = (await meta.GetAsync(containerId, "status_code", ct))["status_code"]?.GetValue<string>();
            switch (status)
            {
                case "FINISHED":
                case "PUBLISHED":
                    return;
                case "ERROR":
                case "EXPIRED":
                    throw new PublishException($"Instagram не прийняв фото (статус {status}). Перевірте формат і пропорції.");
            }
            await Task.Delay(meta.PollDelay, ct);
        }
        throw new PublishException("Instagram довго обробляє фото — спробуйте повторити пізніше.");
    }

    [GeneratedRegex(@"#[\p{L}\p{N}_]+")]
    private static partial Regex Hashtag();
}
