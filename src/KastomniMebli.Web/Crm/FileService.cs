using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace KastomniMebli.Web.Crm;

public sealed class FileStorageOptions
{
    public const string Section = "Files";

    /// <summary>
    /// Корінь сховища. На Render — шлях постійного диска (напр. /var/data/files):
    /// він переживає редеплої, на відміну від файлової системи контейнера.
    /// Локально — App_Data/files у папці проєкту.
    /// </summary>
    public string Root { get; set; } = "App_Data/files";

    public int MaxSizeMb { get; set; } = 20;
}

/// <summary>Креслення й фото до замовлень: вміст на диску, опис — у таблиці order_files.</summary>
public sealed class FileService(IDbContextFactory<AppDbContext> dbs, FileStore store, TimeProvider time, ILogger<FileService> log)
{
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".dwg"] = "application/acad",
        [".dxf"] = "application/dxf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".heic"] = "image/heic",
        [".heif"] = "image/heif",
        [".zip"] = "application/zip",
    };

    public static string AllowedExtensions => string.Join(",", Allowed.Keys);

    public static bool IsAllowed(string fileName) => Allowed.ContainsKey(Path.GetExtension(fileName));

    public long MaxBytes => store.MaxBytes;

    public async Task<OrderFile> UploadAsync(CurrentUser actor, int orderId, string kind, string fileName, Stream content, long size)
    {
        await using var db = await dbs.CreateDbContextAsync();
        await RequireAccessAsync(db, actor, orderId);
        return await SaveAsync(db, orderId, kind, fileName, content, size, actor.Id);
    }

    /// <summary>Без перевірки прав — для файлів із публічної форми (B2B-заявка).</summary>
    public async Task<OrderFile> SaveFromPublicFormAsync(int orderId, string fileName, Stream content, long size)
    {
        await using var db = await dbs.CreateDbContextAsync();
        return await SaveAsync(db, orderId, "brief", fileName, content, size, uploadedBy: null);
    }

    private async Task<OrderFile> SaveAsync(AppDbContext db, int orderId, string kind, string fileName, Stream content, long size, int? uploadedBy)
    {
        if (!Catalog.Has(Catalog.FileKinds, kind))
            throw new CrmException("Невідомий вид файлу.");
        var ext = Path.GetExtension(fileName);
        if (!Allowed.TryGetValue(ext, out var contentType))
            throw new CrmException($"Такий тип файлу не приймаємо. Можна: {AllowedExtensions}.");
        if (size > store.MaxBytes)
            throw new CrmException($"Файл завеликий — до {store.MaxSizeMb} МБ.");

        var relative = await store.SaveAsync($"orders/{orderId}", ext, content, store.MaxBytes);
        var file = new OrderFile
        {
            OrderId = orderId,
            Kind = kind,
            FileName = SafeName(fileName),
            ContentType = contentType,
            Size = store.Size(relative),
            StoragePath = relative,
            UploadedByUserId = uploadedBy,
            UploadedAt = time.GetUtcNow().UtcDateTime,
        };
        db.OrderFiles.Add(file);
        await db.SaveChangesAsync();
        return file;
    }

    /// <summary>Файл для завантаження; null — немає доступу або файлу.</summary>
    public async Task<(OrderFile File, string Path)?> OpenAsync(CurrentUser actor, int fileId)
    {
        await using var db = await dbs.CreateDbContextAsync();
        var file = await db.OrderFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fileId);
        if (file is null || !await HasAccessAsync(db, actor, file.OrderId))
            return null;
        if (!store.Exists(file.StoragePath))
        {
            log.LogError("Файл {FileId} є в базі, але відсутній на диску: {Path}", file.Id, file.StoragePath);
            return null;
        }
        return (file, store.FullPath(file.StoragePath));
    }

    public async Task DeleteAsync(CurrentUser actor, int fileId)
    {
        await using var db = await dbs.CreateDbContextAsync();
        var file = await db.OrderFiles.FirstOrDefaultAsync(f => f.Id == fileId) ?? throw new CrmException("Файл не знайдено.");
        if (!actor.CanSeeMoney && file.UploadedByUserId != actor.Id)
            throw new CrmForbiddenException();
        db.OrderFiles.Remove(file);
        await db.SaveChangesAsync();
        store.TryDelete(file.StoragePath);
    }

    private static async Task<bool> HasAccessAsync(AppDbContext db, CurrentUser actor, int orderId) =>
        actor.CanSeeMoney
            ? await db.Orders.AnyAsync(o => o.Id == orderId)
            : await db.OrderAssignees.AnyAsync(a => a.OrderId == orderId && a.UserId == actor.Id);

    private static async Task RequireAccessAsync(AppDbContext db, CurrentUser actor, int orderId)
    {
        if (!await HasAccessAsync(db, actor, orderId))
            throw new CrmForbiddenException();
    }

    public static string SafeName(string name)
    {
        var clean = Path.GetFileName(name);
        foreach (var c in Path.GetInvalidFileNameChars())
            clean = clean.Replace(c, '_');
        return clean.Length > 200 ? clean[^200..] : clean;
    }
}
