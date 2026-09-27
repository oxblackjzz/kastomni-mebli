using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Leads;
using Microsoft.EntityFrameworkCore;

namespace KastomniMebli.Web.Crm;

public sealed class PortfolioInput
{
    public string Title { get; set; } = "";
    public string Category { get; set; } = "wardrobe";
    public string? Location { get; set; }
    public string? Description { get; set; }
    public bool IsPublished { get; set; }
    public bool OnHome { get; set; }
    public bool ForMakers { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Картка роботи для сайту: обкладинка + кількість фото.</summary>
public sealed record WorkCard(int Id, string Title, string Category, string? Location, int? CoverPhotoId, int PhotoCount);

/// <summary>
/// Портфоліо. Фото стискаються ще в браузері (Blazor RequestImageFileAsync): велике до 1920 px і мініатюра до 720 px,
/// тож на сервер не йдуть 10-мегабайтні фото з телефона, а заодно зникають EXIF-дані (зокрема GPS — адреса клієнта).
/// </summary>
public sealed class PortfolioService(IDbContextFactory<AppDbContext> dbs, FileStore store, TimeProvider time)
{
    public const int HomeSlots = 6;
    public const long MaxPhotoBytes = 8 * 1024 * 1024;

    // ---------- Сайт ----------

    /// <summary>Для головної: спершу позначені «на головній», далі найновіші опубліковані — до 6.</summary>
    public async Task<List<WorkCard>> HomeCardsAsync()
    {
        await using var db = await dbs.CreateDbContextAsync();
        var works = await Published(db)
            .OrderByDescending(w => w.OnHome).ThenBy(w => w.SortOrder).ThenByDescending(w => w.CreatedAt)
            .Take(HomeSlots)
            .ToListAsync();
        return works.Select(ToCard).ToList();
    }

    public async Task<List<WorkCard>> PublishedCardsAsync(string? category = null, bool forMakers = false)
    {
        await using var db = await dbs.CreateDbContextAsync();
        var q = Published(db);
        if (category is not null && FurnitureTypes.IsKnown(category))
            q = q.Where(w => w.Category == category);
        if (forMakers)
            q = q.Where(w => w.ForMakers);
        var works = await q.OrderBy(w => w.SortOrder).ThenByDescending(w => w.CreatedAt).ToListAsync();
        return works.Select(ToCard).ToList();
    }

    public async Task<PortfolioWork?> PublishedWorkAsync(int id)
    {
        await using var db = await dbs.CreateDbContextAsync();
        return await Published(db).FirstOrDefaultAsync(w => w.Id == id);
    }

    /// <summary>Фото для сайту: чернетки видно лише тим, хто в CRM.</summary>
    public async Task<string?> PhotoPathAsync(int photoId, bool thumb, bool includeDrafts)
    {
        await using var db = await dbs.CreateDbContextAsync();
        var photo = await db.PortfolioPhotos.AsNoTracking()
            .Where(p => p.Id == photoId)
            .Where(p => includeDrafts || db.PortfolioWorks.Any(w => w.Id == p.WorkId && w.IsPublished))
            .FirstOrDefaultAsync();
        if (photo is null)
            return null;
        var relative = thumb ? photo.ThumbPath : photo.LargePath;
        return store.Exists(relative) ? store.FullPath(relative) : null;
    }

    private static IQueryable<PortfolioWork> Published(AppDbContext db) =>
        db.PortfolioWorks.AsNoTracking()
            .Include(w => w.Photos.OrderBy(p => p.SortOrder).ThenBy(p => p.Id))
            .Where(w => w.IsPublished && w.Photos.Any());

    private static WorkCard ToCard(PortfolioWork w)
    {
        var cover = w.Photos.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).FirstOrDefault();
        return new WorkCard(w.Id, w.Title, w.Category, w.Location, cover?.Id, w.Photos.Count);
    }

    // ---------- CRM ----------

    public async Task<List<PortfolioWork>> ListAsync(CurrentUser actor)
    {
        RequireEditor(actor);
        await using var db = await dbs.CreateDbContextAsync();
        return await db.PortfolioWorks.AsNoTracking()
            .Include(w => w.Photos.OrderBy(p => p.SortOrder).ThenBy(p => p.Id))
            .OrderBy(w => w.SortOrder).ThenByDescending(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task<PortfolioWork> GetAsync(CurrentUser actor, int id)
    {
        RequireEditor(actor);
        await using var db = await dbs.CreateDbContextAsync();
        return await db.PortfolioWorks.AsNoTracking()
            .Include(w => w.Photos.OrderBy(p => p.SortOrder).ThenBy(p => p.Id))
            .FirstOrDefaultAsync(w => w.Id == id) ?? throw new CrmException("Роботу не знайдено.");
    }

    public async Task<int> CreateAsync(CurrentUser actor, PortfolioInput input)
    {
        RequireEditor(actor);
        Validate(input);
        await using var db = await dbs.CreateDbContextAsync();
        var now = time.GetUtcNow().UtcDateTime;
        var work = new PortfolioWork { CreatedAt = now };
        Apply(work, input, now);
        db.PortfolioWorks.Add(work);
        await db.SaveChangesAsync();
        return work.Id;
    }

    public async Task UpdateAsync(CurrentUser actor, int id, PortfolioInput input)
    {
        RequireEditor(actor);
        Validate(input);
        await using var db = await dbs.CreateDbContextAsync();
        var work = await db.PortfolioWorks.FirstOrDefaultAsync(w => w.Id == id) ?? throw new CrmException("Роботу не знайдено.");
        Apply(work, input, time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(CurrentUser actor, int id)
    {
        RequireEditor(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var work = await db.PortfolioWorks.Include(w => w.Photos).FirstOrDefaultAsync(w => w.Id == id)
            ?? throw new CrmException("Роботу не знайдено.");
        db.PortfolioWorks.Remove(work);
        await db.SaveChangesAsync();
        foreach (var p in work.Photos)
        {
            store.TryDelete(p.LargePath);
            store.TryDelete(p.ThumbPath);
        }
    }

    /// <summary>Додати фото (вже стиснуті в браузері: велике й мініатюра).</summary>
    public async Task AddPhotoAsync(CurrentUser actor, int workId, Stream large, Stream thumb)
    {
        RequireEditor(actor);
        await using var db = await dbs.CreateDbContextAsync();
        if (!await db.PortfolioWorks.AnyAsync(w => w.Id == workId))
            throw new CrmException("Роботу не знайдено.");

        var largePath = await store.SaveAsync($"portfolio/{workId}", ".jpg", large, MaxPhotoBytes);
        string thumbPath;
        try
        {
            thumbPath = await store.SaveAsync($"portfolio/{workId}", ".jpg", thumb, MaxPhotoBytes);
        }
        catch
        {
            store.TryDelete(largePath);
            throw;
        }

        var next = await db.PortfolioPhotos.Where(p => p.WorkId == workId).Select(p => (int?)p.SortOrder).MaxAsync() ?? -1;
        db.PortfolioPhotos.Add(new PortfolioPhoto
        {
            WorkId = workId,
            LargePath = largePath,
            ThumbPath = thumbPath,
            SortOrder = next + 1,
            UploadedAt = time.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync();
    }

    public async Task DeletePhotoAsync(CurrentUser actor, int photoId)
    {
        RequireEditor(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var photo = await db.PortfolioPhotos.FirstOrDefaultAsync(p => p.Id == photoId) ?? throw new CrmException("Фото не знайдено.");
        db.PortfolioPhotos.Remove(photo);
        await db.SaveChangesAsync();
        store.TryDelete(photo.LargePath);
        store.TryDelete(photo.ThumbPath);
    }

    /// <summary>Посунути фото на позицію вперед (-1) чи назад (+1). Перше фото — обкладинка.</summary>
    public async Task MovePhotoAsync(CurrentUser actor, int photoId, int direction)
    {
        RequireEditor(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var photo = await db.PortfolioPhotos.FirstOrDefaultAsync(p => p.Id == photoId) ?? throw new CrmException("Фото не знайдено.");
        var photos = await db.PortfolioPhotos.Where(p => p.WorkId == photo.WorkId).OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync();
        var i = photos.FindIndex(p => p.Id == photoId);
        var j = Math.Clamp(i + Math.Sign(direction), 0, photos.Count - 1);
        (photos[i], photos[j]) = (photos[j], photos[i]);
        for (var k = 0; k < photos.Count; k++)
            photos[k].SortOrder = k;
        await db.SaveChangesAsync();
    }

    public async Task SetCaptionAsync(CurrentUser actor, int photoId, string? caption)
    {
        RequireEditor(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var photo = await db.PortfolioPhotos.FirstOrDefaultAsync(p => p.Id == photoId) ?? throw new CrmException("Фото не знайдено.");
        photo.Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim()[..Math.Min(caption.Trim().Length, 300)];
        await db.SaveChangesAsync();
    }

    private static void Validate(PortfolioInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 120)
            throw new CrmException("Вкажіть назву (до 120 символів).");
        if (!FurnitureTypes.IsKnown(input.Category))
            throw new CrmException("Вкажіть тип меблів.");
        if (input.Description is { Length: > 2000 })
            throw new CrmException("Опис задовгий (до 2000 символів).");
    }

    private static void Apply(PortfolioWork work, PortfolioInput input, DateTime now)
    {
        work.Title = input.Title.Trim();
        work.Category = input.Category;
        work.Location = string.IsNullOrWhiteSpace(input.Location) ? null : input.Location.Trim();
        work.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        work.IsPublished = input.IsPublished;
        work.OnHome = input.OnHome;
        work.ForMakers = input.ForMakers;
        work.SortOrder = input.SortOrder;
        work.UpdatedAt = now;
    }

    private static void RequireEditor(CurrentUser actor)
    {
        if (!actor.CanSeeMoney)
            throw new CrmForbiddenException();
    }
}
