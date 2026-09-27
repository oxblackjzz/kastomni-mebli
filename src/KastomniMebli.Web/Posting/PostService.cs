using System.Security.Cryptography;
using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace KastomniMebli.Web.Posting;

public sealed record ChannelInfo(string Network, string Label, string? ConfigProblem);

/// <summary>Пости для соцмереж: чернетка → черга (зараз чи за розкладом) → публікує PostPublisher.</summary>
public sealed class PostService(
    IDbContextFactory<AppDbContext> dbs,
    IEnumerable<IPostChannel> channels,
    FileStore store,
    TimeProvider time)
{
    public const int MaxPhotos = 10;
    public const int TextMax = 4096;
    public const long MaxPhotoBytes = 8 * 1024 * 1024;

    public IReadOnlyList<ChannelInfo> Channels() =>
        Networks.All.Select(n => new ChannelInfo(n.Key, n.Label, Channel(n.Key)?.ConfigProblem ?? "ще не підключено")).ToList();

    /// <summary>Для кожної мережі: чому цей пост туди не піде (null — піде).</summary>
    public Dictionary<string, string?> Check(string text, int photoCount) =>
        Networks.All.ToDictionary(n => n.Key, n =>
            Channel(n.Key) is not { } c ? "ще не підключено" : c.ConfigProblem ?? c.CheckPost(text.Trim(), photoCount));

    public async Task<List<Post>> ListAsync(CurrentUser actor)
    {
        Require(actor);
        await using var db = await dbs.CreateDbContextAsync();
        return await db.Posts.AsNoTracking()
            .Include(p => p.Photos.OrderBy(x => x.SortOrder).ThenBy(x => x.Id))
            .Include(p => p.Targets)
            .OrderByDescending(p => p.CreatedAt)
            .Take(200)
            .ToListAsync();
    }

    public async Task<Post> GetAsync(CurrentUser actor, int id)
    {
        Require(actor);
        await using var db = await dbs.CreateDbContextAsync();
        return await db.Posts.AsNoTracking()
            .Include(p => p.Photos.OrderBy(x => x.SortOrder).ThenBy(x => x.Id))
            .Include(p => p.Targets)
            .FirstOrDefaultAsync(p => p.Id == id) ?? throw new CrmException("Пост не знайдено.");
    }

    public async Task<int> CreateAsync(CurrentUser actor)
    {
        Require(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var now = time.GetUtcNow().UtcDateTime;
        var post = new Post { CreatedByUserId = actor.Id, CreatedAt = now, UpdatedAt = now };
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        return post.Id;
    }

    public async Task SaveTextAsync(CurrentUser actor, int id, string text)
    {
        Require(actor);
        if (text.Length > TextMax)
            throw new CrmException($"Текст задовгий (до {TextMax} символів).");
        await using var db = await dbs.CreateDbContextAsync();
        var post = await EditableAsync(db, id);
        post.Text = text.Replace("\r\n", "\n");
        post.UpdatedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
    }

    /// <summary>Фото вже підготовлене в браузері: JPEG, обрізаний під Instagram (4:5…1.91:1), до 1440 px.</summary>
    public async Task AddPhotoAsync(CurrentUser actor, int id, Stream jpeg)
    {
        Require(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var post = await EditableAsync(db, id);
        var count = await db.PostPhotos.CountAsync(p => p.PostId == post.Id);
        if (count >= MaxPhotos)
            throw new CrmException($"Не більше {MaxPhotos} фото.");
        var path = await store.SaveAsync($"posts/{post.Id}", ".jpg", jpeg, MaxPhotoBytes);
        db.PostPhotos.Add(new PostPhoto
        {
            PostId = post.Id,
            Path = path,
            PublicKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
            SortOrder = count,
        });
        await db.SaveChangesAsync();
    }

    public async Task DeletePhotoAsync(CurrentUser actor, int photoId)
    {
        Require(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var photo = await db.PostPhotos.FirstOrDefaultAsync(p => p.Id == photoId) ?? throw new CrmException("Фото не знайдено.");
        await EditableAsync(db, photo.PostId);
        db.PostPhotos.Remove(photo);
        await db.SaveChangesAsync();
        store.TryDelete(photo.Path);
    }

    public async Task MovePhotoAsync(CurrentUser actor, int photoId, int direction)
    {
        Require(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var photo = await db.PostPhotos.FirstOrDefaultAsync(p => p.Id == photoId) ?? throw new CrmException("Фото не знайдено.");
        await EditableAsync(db, photo.PostId);
        var photos = await db.PostPhotos.Where(p => p.PostId == photo.PostId).OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync();
        var i = photos.FindIndex(p => p.Id == photoId);
        var j = Math.Clamp(i + Math.Sign(direction), 0, photos.Count - 1);
        (photos[i], photos[j]) = (photos[j], photos[i]);
        for (var k = 0; k < photos.Count; k++)
            photos[k].SortOrder = k;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Поставити в чергу: зараз (whenLocal = null) або на київський час. Для кожної обраної мережі —
    /// окрема публікація; мережа, куди пост не підходить, відхиляється одразу з поясненням.
    /// </summary>
    public async Task QueueAsync(CurrentUser actor, int id, IReadOnlyCollection<string> networks, DateTime? whenLocal)
    {
        Require(actor);
        if (networks.Count == 0)
            throw new CrmException("Оберіть, куди публікувати.");
        await using var db = await dbs.CreateDbContextAsync();
        var post = await EditableAsync(db, id);
        var photos = await db.PostPhotos.CountAsync(p => p.PostId == id);

        var problems = Check(post.Text, photos);
        var bad = networks.Where(n => !problems.ContainsKey(n) || problems[n] is not null).ToList();
        if (bad.Count > 0)
            throw new CrmException(string.Join("; ", bad.Select(n => $"{Networks.Label(n)}: {problems.GetValueOrDefault(n) ?? "невідома мережа"}")));

        var now = time.GetUtcNow().UtcDateTime;
        DateTime? when = whenLocal is { } local ? Kyiv.ToUtc(local) : null;
        if (when is { } w && w < now.AddMinutes(-1))
            throw new CrmException("Час публікації вже минув.");

        db.PostTargets.RemoveRange(db.PostTargets.Where(t => t.PostId == id));
        foreach (var network in networks.Distinct())
            db.PostTargets.Add(new PostTarget { PostId = id, Network = network, Status = PostTargetStatuses.Pending });
        post.IsDraft = false;
        post.ScheduledAt = when;
        post.UpdatedAt = now;
        await db.SaveChangesAsync();
    }

    /// <summary>Зняти з розкладу (якщо ще нічого не опубліковано) — знову чернетка.</summary>
    public async Task UnqueueAsync(CurrentUser actor, int id)
    {
        Require(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var post = await db.Posts.Include(p => p.Targets).FirstOrDefaultAsync(p => p.Id == id) ?? throw new CrmException("Пост не знайдено.");
        if (post.Targets.Any(t => t.Status is PostTargetStatuses.Done or PostTargetStatuses.Publishing))
            throw new CrmException("Пост уже публікується або опублікований — зняти не можна.");
        db.PostTargets.RemoveRange(post.Targets);
        post.IsDraft = true;
        post.ScheduledAt = null;
        await db.SaveChangesAsync();
    }

    /// <summary>Повторити невдалу публікацію в одну мережу.</summary>
    public async Task RetryAsync(CurrentUser actor, int targetId)
    {
        Require(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var target = await db.PostTargets.FirstOrDefaultAsync(t => t.Id == targetId) ?? throw new CrmException("Не знайдено.");
        if (target.Status != PostTargetStatuses.Failed)
            throw new CrmException("Повторити можна лише невдалу публікацію.");
        target.Status = PostTargetStatuses.Pending;
        target.Error = null;
        await db.SaveChangesAsync();
    }

    /// <summary>Видаляє пост лише з журналу — з самих мереж нічого не видаляється.</summary>
    public async Task DeleteAsync(CurrentUser actor, int id)
    {
        Require(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var post = await db.Posts.Include(p => p.Photos).Include(p => p.Targets).FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new CrmException("Пост не знайдено.");
        if (post.Targets.Any(t => t.Status == PostTargetStatuses.Publishing))
            throw new CrmException("Пост саме публікується — зачекайте хвилину.");
        db.Posts.Remove(post);
        await db.SaveChangesAsync();
        foreach (var p in post.Photos)
            store.TryDelete(p.Path);
    }

    public async Task<string?> PublicPhotoPathAsync(string key)
    {
        await using var db = await dbs.CreateDbContextAsync();
        var photo = await db.PostPhotos.AsNoTracking().FirstOrDefaultAsync(p => p.PublicKey == key);
        return photo is not null && store.Exists(photo.Path) ? store.FullPath(photo.Path) : null;
    }

    private IPostChannel? Channel(string network) => channels.FirstOrDefault(c => c.Network == network);

    /// <summary>Змінювати можна, поки пост не почав публікуватись.</summary>
    private static async Task<Post> EditableAsync(AppDbContext db, int id)
    {
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id) ?? throw new CrmException("Пост не знайдено.");
        if (await db.PostTargets.AnyAsync(t => t.PostId == id && t.Status != PostTargetStatuses.Pending))
            throw new CrmException("Пост уже публікувався — змінювати не можна. Створіть новий.");
        if (!post.IsDraft)
            throw new CrmException("Пост у черзі. Спершу зніміть його з розкладу.");
        return post;
    }

    private static void Require(CurrentUser actor)
    {
        if (!actor.CanSeeMoney)
            throw new CrmForbiddenException();
    }
}
