using System.Net;
using System.Text;
using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Posting;
using KastomniMebli.Web.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KastomniMebli.Tests;

/// <summary>Підставний HTTP: записує запити й віддає заготовлені відповіді по черзі.</summary>
public sealed class FakeHttp : HttpMessageHandler
{
    public List<(string Method, string Path, string Body)> Requests { get; } = [];
    private readonly Queue<(HttpStatusCode Code, string Json)> _responses = new();

    public FakeHttp Reply(string json, HttpStatusCode code = HttpStatusCode.OK)
    {
        _responses.Enqueue((code, json));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        Requests.Add((request.Method.Method, request.RequestUri!.PathAndQuery, body));
        var (code, json) = _responses.Count > 0 ? _responses.Dequeue() : (HttpStatusCode.OK, "{}");
        return new HttpResponseMessage(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}

public class PostingTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value)).Build();

    private static (PostMedia Media, string Root) Media(string baseUrl = "https://site.example")
    {
        var root = Path.Combine(Path.GetTempPath(), "km-post-" + Guid.NewGuid().ToString("N"));
        var store = new FileStore(Options.Create(new FileStorageOptions { Root = root }), null!, NullLogger<FileStore>.Instance);
        return (new PostMedia(Options.Create(new SiteSettings { BaseUrl = baseUrl }), store), root);
    }

    private static List<PostPhoto> Photos(string root, int count)
    {
        Directory.CreateDirectory(Path.Combine(root, "posts"));
        return Enumerable.Range(0, count).Select(i =>
        {
            var rel = $"posts/p{i}.jpg";
            File.WriteAllBytes(Path.Combine(root, rel), [0xFF, 0xD8, (byte)i]);
            return new PostPhoto { Id = i + 1, Path = rel, PublicKey = new string((char)('a' + i), 32), SortOrder = i };
        }).ToList();
    }

    [Fact]
    public async Task Telegram_album_with_long_text_sends_text_as_reply()
    {
        var (media, root) = Media();
        var http = new FakeHttp()
            .Reply("""{"ok":true,"result":[{"message_id":41},{"message_id":42}]}""")
            .Reply("""{"ok":true,"result":{"message_id":43}}""");
        var channel = new TelegramChannel(new HttpClient(http) { BaseAddress = new Uri("https://api.telegram.org/") },
            Config(("TELEGRAM_BOT_TOKEN", "T"), ("TELEGRAM_CHANNEL_ID", "@kastomni")), media);

        var text = "<b>" + new string('а', 1100);
        var result = await channel.PublishAsync(new Post { Text = text }, Photos(root, 2), default);

        Assert.Equal("41", result.ExternalId);
        Assert.Equal("https://t.me/kastomni/41", result.Url);
        Assert.Equal("/botT/sendMediaGroup", http.Requests[0].Path);
        Assert.DoesNotContain("caption", http.Requests[0].Body);          // підпис не влазить
        Assert.Equal("/botT/sendMessage", http.Requests[1].Path);
        Assert.Contains("\"message_id\":41", http.Requests[1].Body);     // відповідь на альбом
        var sent = System.Text.Json.Nodes.JsonNode.Parse(http.Requests[1].Body)!["text"]!.GetValue<string>();
        Assert.StartsWith("&lt;b&gt;", sent);                             // текст екранований під HTML-режим
    }

    [Fact]
    public async Task Telegram_error_is_readable()
    {
        var (media, _) = Media();
        var http = new FakeHttp().Reply("""{"ok":false,"description":"Forbidden: bot is not a member of the channel chat"}""", HttpStatusCode.Forbidden);
        var channel = new TelegramChannel(new HttpClient(http) { BaseAddress = new Uri("https://api.telegram.org/") },
            Config(("TELEGRAM_BOT_TOKEN", "T"), ("TELEGRAM_CHANNEL_ID", "@kastomni")), media);

        var ex = await Assert.ThrowsAsync<PublishException>(() => channel.PublishAsync(new Post { Text = "Привіт" }, [], default));
        Assert.Contains("bot is not a member", ex.Message);
    }

    [Fact]
    public async Task Facebook_multi_photo_uploads_unpublished_then_feed()
    {
        var (media, root) = Media();
        var http = new FakeHttp()
            .Reply("""{"id":"ph1"}""").Reply("""{"id":"ph2"}""").Reply("""{"id":"page_post"}""");
        var meta = new MetaGraph(new HttpClient(http) { BaseAddress = new Uri("https://graph.facebook.com/") },
            Config(("FB_PAGE_ID", "123"), ("FB_PAGE_ACCESS_TOKEN", "SECRET")));
        var channel = new FacebookChannel(meta, media);

        var result = await channel.PublishAsync(new Post { Text = "Нова шафа" }, Photos(root, 2), default);

        Assert.Equal("page_post", result.ExternalId);
        Assert.All(http.Requests, r => Assert.StartsWith("/v26.0/123/", r.Path));
        Assert.All(http.Requests, r => Assert.DoesNotContain("SECRET", r.Path));   // токен не в адресі
        Assert.Contains("published=false", http.Requests[0].Body);
        Assert.Contains(Uri.EscapeDataString("https://site.example/media/posty/"), http.Requests[0].Body);
        Assert.EndsWith("/feed", http.Requests[2].Path);
        Assert.Contains(Uri.EscapeDataString("{\"media_fbid\":\"ph2\"}"), http.Requests[2].Body);
    }

    [Fact]
    public async Task Instagram_carousel_waits_for_container_then_publishes()
    {
        var (media, root) = Media();
        var http = new FakeHttp()
            .Reply("""{"id":"c1"}""").Reply("""{"id":"c2"}""")          // дочірні контейнери
            .Reply("""{"id":"carousel"}""")                              // карусель
            .Reply("""{"status_code":"IN_PROGRESS"}""").Reply("""{"status_code":"FINISHED"}""")
            .Reply("""{"id":"media9"}""")                                // media_publish
            .Reply("""{"permalink":"https://www.instagram.com/p/xyz/"}""");
        var meta = new MetaGraph(new HttpClient(http) { BaseAddress = new Uri("https://graph.facebook.com/") },
            Config(("IG_USER_ID", "ig1"), ("FB_PAGE_ACCESS_TOKEN", "SECRET"))) { PollDelay = TimeSpan.Zero };
        var channel = new InstagramChannel(meta, media);

        var result = await channel.PublishAsync(new Post { Text = "Кухня #меблі" }, Photos(root, 2), default);

        Assert.Equal("media9", result.ExternalId);
        Assert.Equal("https://www.instagram.com/p/xyz/", result.Url);
        Assert.Contains("is_carousel_item=true", http.Requests[0].Body);
        Assert.Contains("media_type=CAROUSEL", http.Requests[2].Body);
        Assert.Contains("children=c1%2Cc2", http.Requests[2].Body);
        Assert.Equal("/v26.0/ig1/media_publish", http.Requests[5].Path);
        Assert.Contains("creation_id=carousel", http.Requests[5].Body);
    }

    [Theory]
    [InlineData(0, "", "без фото")]
    [InlineData(1, "x", null)]
    [InlineData(11, "x", "10 фото")]
    public void Instagram_checks(int photos, string text, string? problem)
    {
        var (media, _) = Media();
        var channel = new InstagramChannel(new MetaGraph(new HttpClient(), Config()), media);

        var result = channel.CheckPost(text, photos);

        if (problem is null)
            Assert.Null(result);
        else
            Assert.Contains(problem, result);
    }

    [Fact]
    public void Instagram_rejects_too_many_hashtags_and_needs_https()
    {
        var (media, _) = Media();
        var channel = new InstagramChannel(new MetaGraph(new HttpClient(), Config()), media);
        var tags = string.Join(" ", Enumerable.Range(0, 31).Select(i => "#тег" + i));

        Assert.Contains("хештегів", channel.CheckPost(tags, 1));
        var (httpMedia, _) = Media("http://localhost:5155");
        Assert.Contains("https", new InstagramChannel(new MetaGraph(new HttpClient(), Config()), httpMedia).CheckPost("x", 1));
    }

    /// <summary>Фейковий канал: успіх або помилка.</summary>
    private sealed class FakeChannel(string network, bool fail) : IPostChannel
    {
        public int Calls { get; private set; }
        public string Network => network;
        public string? ConfigProblem => null;
        public string? CheckPost(string text, int photoCount) => null;

        public Task<PublishResult> PublishAsync(Post post, IReadOnlyList<PostPhoto> photos, CancellationToken ct)
        {
            Calls++;
            return fail ? throw new PublishException("Meta: Invalid OAuth access token") : Task.FromResult(new PublishResult("ext1", null));
        }
    }

    [Fact]
    public async Task One_network_failing_does_not_stop_others_and_admin_is_told()
    {
        var telegram = new FakeChannel(Networks.Telegram, fail: false);
        var facebook = new FakeChannel(Networks.Facebook, fail: true);
        await using var app = new SiteFactory
        {
            ExtraServices = s =>
            {
                s.RemoveAll<IPostChannel>();
                s.AddSingleton<IPostChannel>(telegram);
                s.AddSingleton<IPostChannel>(facebook);
            },
        };
        using var scope = app.Services.CreateScope();
        var posts = scope.ServiceProvider.GetRequiredService<PostService>();
        var admin = new CurrentUser(0, "a", Roles.Admin);

        var id = await posts.CreateAsync(admin);
        await posts.SaveTextAsync(admin, id, "Нова кухня");
        await posts.QueueAsync(admin, id, [Networks.Telegram, Networks.Facebook], whenLocal: null);

        var publisher = new PostPublisher(app.Services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, NullLogger<PostPublisher>.Instance);
        Assert.Equal(2, await publisher.PublishDueAsync());
        Assert.Equal(0, await publisher.PublishDueAsync());   // вдруге нічого

        var post = await posts.GetAsync(admin, id);
        Assert.Equal(PostTargetStatuses.Done, post.Targets.Single(t => t.Network == Networks.Telegram).Status);
        var fb = post.Targets.Single(t => t.Network == Networks.Facebook);
        Assert.Equal(PostTargetStatuses.Failed, fb.Status);
        Assert.Contains("OAuth", fb.Error);
        Assert.Contains(app.Telegram.Sent, m => m.Text.Contains("не опубліковано") && m.Text.Contains("Facebook"));

        // Повтор: лише Facebook.
        await posts.RetryAsync(admin, fb.Id);
        await publisher.PublishDueAsync();
        Assert.Equal(1, telegram.Calls);
        Assert.Equal(2, facebook.Calls);
    }

    [Fact]
    public async Task Scheduled_post_waits_for_its_time()
    {
        var channel = new FakeChannel(Networks.Telegram, fail: false);
        await using var app = new SiteFactory
        {
            ExtraServices = s =>
            {
                s.RemoveAll<IPostChannel>();
                s.AddSingleton<IPostChannel>(channel);
            },
        };
        using var scope = app.Services.CreateScope();
        var posts = scope.ServiceProvider.GetRequiredService<PostService>();
        var admin = new CurrentUser(0, "a", Roles.Admin);

        var id = await posts.CreateAsync(admin);
        await posts.SaveTextAsync(admin, id, "Завтра");
        await posts.QueueAsync(admin, id, [Networks.Telegram], Kyiv.ToLocal(DateTime.UtcNow.AddHours(2)));

        var publisher = new PostPublisher(app.Services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, NullLogger<PostPublisher>.Instance);
        Assert.Equal(0, await publisher.PublishDueAsync());
        Assert.Equal(0, channel.Calls);
    }

    [Fact]
    public async Task Queue_rejects_networks_that_cannot_take_the_post()
    {
        await using var f = new SiteFactory();
        using var scope = f.Services.CreateScope();
        var posts = scope.ServiceProvider.GetRequiredService<PostService>();
        var admin = new CurrentUser(0, "a", Roles.Admin);
        var id = await posts.CreateAsync(admin);
        await posts.SaveTextAsync(admin, id, "Текст без фото");

        // Instagram без фото й без налаштувань — відмова з поясненням.
        var ex = await Assert.ThrowsAsync<CrmException>(() => posts.QueueAsync(admin, id, [Networks.Instagram], null));
        Assert.Contains("Instagram", ex.Message);
    }
}
