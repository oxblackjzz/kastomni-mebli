using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Notifications;

namespace KastomniMebli.Web.Posting;

/// <summary>
/// Telegram-канал через Bot API. Бот (TELEGRAM_BOT_TOKEN) — адміністратор каналу з правом «Публікація повідомлень».
/// TELEGRAM_CHANNEL_ID — @назва_каналу або числовий id (-100…).
/// Ліміти: текст до 4096 символів, підпис до фото — до 1024, в альбомі 2–10 фото.
/// Якщо текст довший за підпис — спершу фото, потім текст окремим повідомленням-відповіддю.
/// </summary>
public sealed class TelegramChannel(HttpClient http, IConfiguration config, PostMedia media) : IPostChannel
{
    public const int CaptionMax = 1024;
    public const int TextMax = 4096;

    public string Network => Networks.Telegram;

    private string? Token => config["TELEGRAM_BOT_TOKEN"];
    private string? ChannelId => config["TELEGRAM_CHANNEL_ID"]?.Trim();

    public string? ConfigProblem =>
        string.IsNullOrWhiteSpace(Token) ? "немає TELEGRAM_BOT_TOKEN"
        : string.IsNullOrWhiteSpace(ChannelId) ? "немає TELEGRAM_CHANNEL_ID"
        : null;

    public string? CheckPost(string text, int photoCount) =>
        text.Length > TextMax ? $"текст довший за {TextMax} символів"
        : photoCount > 10 ? "не більше 10 фото"
        : text.Length == 0 && photoCount == 0 ? "порожній пост"
        : null;

    public async Task<PublishResult> PublishAsync(Post post, IReadOnlyList<PostPhoto> photos, CancellationToken ct)
    {
        if (ConfigProblem is { } problem)
            throw new PublishException("Telegram не налаштовано: " + problem);

        var text = post.Text.Trim();
        var html = TelegramMessage.Escape(text);
        var captionFits = text.Length <= CaptionMax;
        long firstMessageId;

        if (photos.Count == 0)
        {
            firstMessageId = (await CallAsync("sendMessage", Json(new JsonObject
            {
                ["chat_id"] = ChannelId,
                ["text"] = html,
                ["parse_mode"] = "HTML",
            }), ct))["message_id"]!.GetValue<long>();
        }
        else
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(ChannelId!), "chat_id");
            if (photos.Count == 1)
            {
                AddFile(form, "photo", media.LocalPath(photos[0]));
                if (captionFits && text.Length > 0)
                {
                    form.Add(new StringContent(html), "caption");
                    form.Add(new StringContent("HTML"), "parse_mode");
                }
                firstMessageId = (await CallAsync("sendPhoto", form, ct))["message_id"]!.GetValue<long>();
            }
            else
            {
                var items = new JsonArray();
                for (var i = 0; i < photos.Count; i++)
                {
                    var item = new JsonObject { ["type"] = "photo", ["media"] = $"attach://p{i}" };
                    if (i == 0 && captionFits && text.Length > 0)
                    {
                        item["caption"] = html;
                        item["parse_mode"] = "HTML";
                    }
                    items.Add(item);
                    AddFile(form, $"p{i}", media.LocalPath(photos[i]));
                }
                form.Add(new StringContent(items.ToJsonString()), "media");
                firstMessageId = (await CallAsync("sendMediaGroup", form, ct)).AsArray()[0]!["message_id"]!.GetValue<long>();
            }

            // Довгий текст — окремим повідомленням-відповіддю на фото.
            if (!captionFits)
            {
                await CallAsync("sendMessage", Json(new JsonObject
                {
                    ["chat_id"] = ChannelId,
                    ["text"] = html,
                    ["parse_mode"] = "HTML",
                    ["reply_parameters"] = new JsonObject { ["message_id"] = firstMessageId },
                }), ct);
            }
        }

        var url = ChannelId!.StartsWith('@') ? $"https://t.me/{ChannelId[1..]}/{firstMessageId}" : null;
        return new PublishResult(firstMessageId.ToString(), url);
    }

    private static StringContent Json(JsonObject body) =>
        new(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");

    private static void AddFile(MultipartFormDataContent form, string name, string path)
    {
        var file = new StreamContent(File.OpenRead(path));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, name, Path.GetFileName(path));
    }

    private async Task<JsonNode> CallAsync(string method, HttpContent content, CancellationToken ct)
    {
        using var response = await http.PostAsync($"bot{Token}/{method}", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        JsonNode? json;
        try
        {
            json = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            throw new PublishException($"Telegram {(int)response.StatusCode}: незрозуміла відповідь");
        }
        if (json?["ok"]?.GetValue<bool>() == true && json["result"] is { } result)
            return result;

        var description = json?["description"]?.GetValue<string>() ?? response.ReasonPhrase;
        if (json?["parameters"]?["retry_after"] is { } retry)
            description += $" (Telegram просить зачекати {retry} с)";
        throw new PublishException($"Telegram: {description}");
    }
}
