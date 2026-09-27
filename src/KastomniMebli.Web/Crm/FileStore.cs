using Microsoft.Extensions.Options;

namespace KastomniMebli.Web.Crm;

/// <summary>
/// Файли на диску (на Render — постійний диск). Шляхи в базі — відносні, напр. portfolio/3/ab12….jpg.
/// </summary>
public sealed class FileStore(IOptions<FileStorageOptions> options, IWebHostEnvironment env, ILogger<FileStore> log)
{
    public string RootPath { get; } =
        Path.IsPathRooted(options.Value.Root) ? options.Value.Root : Path.Combine(env.ContentRootPath, options.Value.Root);

    public long MaxBytes => options.Value.MaxSizeMb * 1024L * 1024L;

    public int MaxSizeMb => options.Value.MaxSizeMb;

    /// <summary>Записати потік у новий файл; повертає відносний шлях.</summary>
    public async Task<string> SaveAsync(string folder, string extension, Stream content, long maxBytes)
    {
        var relative = $"{folder.Trim('/')}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var full = FullPath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        try
        {
            await using var target = File.Create(full);
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await content.ReadAsync(buffer)) > 0)
            {
                total += read;
                if (total > maxBytes)
                    throw new CrmException($"Файл завеликий — до {maxBytes / 1024 / 1024} МБ.");
                await target.WriteAsync(buffer.AsMemory(0, read));
            }
        }
        catch
        {
            TryDelete(relative);
            throw;
        }
        return relative;
    }

    public string FullPath(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(RootPath, relative));
        // Захист від «../» у шляху з бази.
        if (!full.StartsWith(Path.GetFullPath(RootPath), StringComparison.Ordinal))
            throw new InvalidOperationException("Шлях поза сховищем файлів");
        return full;
    }

    public bool Exists(string relative) => File.Exists(FullPath(relative));

    public long Size(string relative) => new FileInfo(FullPath(relative)).Length;

    public void TryDelete(string relative)
    {
        try
        {
            File.Delete(FullPath(relative));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.LogWarning(ex, "Не вдалося видалити файл {Path}", relative);
        }
    }
}
