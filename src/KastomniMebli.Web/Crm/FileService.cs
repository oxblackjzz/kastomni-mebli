using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
public sealed class FileService(
    IDbContextFactory<AppDbContext> dbs,
    IOptions<FileStorageOptions> options,
    IWebHostEnvironment env,
    TimeProvider time,
    ILogger<FileService> log)
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
    };

    public static string AllowedExtensions => string.Join(",", Allowed.Keys);

    public long MaxBytes => options.Value.MaxSizeMb * 1024L * 1024L;

    public string RootPath =>
        Path.IsPathRooted(options.Value.Root) ? options.Value.Root : Path.Combine(env.ContentRootPath, options.Value.Root);

    public async Task<OrderFile> UploadAsync(CurrentUser actor, int orderId, string kind, string fileName, Stream content, long size)
    {
        if (!Catalog.Has(Catalog.FileKinds, kind))
            throw new CrmException("Невідомий вид файлу.");
        var ext = Path.GetExtension(fileName);
        if (!Allowed.TryGetValue(ext, out var contentType))
            throw new CrmException($"Такий тип файлу не приймаємо. Можна: {AllowedExtensions}.");
        if (size > MaxBytes)
            throw new CrmException($"Файл завеликий — до {options.Value.MaxSizeMb} МБ.");

        await using var db = await dbs.CreateDbContextAsync();
        await RequireAccessAsync(db, actor, orderId);

        var relative = $"orders/{orderId}/{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var fullPath = Path.Combine(RootPath, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using (var target = File.Create(fullPath))
            await content.CopyToAsync(target);

        var file = new OrderFile
        {
            OrderId = orderId,
            Kind = kind,
            FileName = SafeName(fileName),
            ContentType = contentType,
            Size = new FileInfo(fullPath).Length,
            StoragePath = relative,
            UploadedByUserId = actor.Id,
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
        var path = Path.Combine(RootPath, file.StoragePath);
        if (!File.Exists(path))
        {
            log.LogError("Файл {FileId} є в базі, але відсутній на диску: {Path}", file.Id, file.StoragePath);
            return null;
        }
        return (file, path);
    }

    public async Task DeleteAsync(CurrentUser actor, int fileId)
    {
        await using var db = await dbs.CreateDbContextAsync();
        var file = await db.OrderFiles.FirstOrDefaultAsync(f => f.Id == fileId) ?? throw new CrmException("Файл не знайдено.");
        if (!actor.CanSeeMoney && file.UploadedByUserId != actor.Id)
            throw new CrmForbiddenException();
        db.OrderFiles.Remove(file);
        await db.SaveChangesAsync();
        try
        {
            File.Delete(Path.Combine(RootPath, file.StoragePath));
        }
        catch (IOException ex)
        {
            log.LogWarning(ex, "Не вдалося видалити файл {Path} з диска", file.StoragePath);
        }
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

    private static string SafeName(string name)
    {
        var clean = Path.GetFileName(name);
        foreach (var c in Path.GetInvalidFileNameChars())
            clean = clean.Replace(c, '_');
        return clean.Length > 200 ? clean[^200..] : clean;
    }
}
