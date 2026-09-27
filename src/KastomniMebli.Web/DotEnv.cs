namespace KastomniMebli.Web;

/// <summary>
/// Для локальної розробки: читає KEY=VALUE з файлу .env (шукає в поточній папці й вище)
/// у змінні оточення. Уже задані змінні не перезаписує. На Render .env немає — там змінні з панелі.
/// </summary>
public static class DotEnv
{
    public static void Load()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; i < 4 && dir is not null; i++, dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, ".env");
            if (!File.Exists(path))
                continue;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;
                var eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;
                var key = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim().Trim('"');
                if (Environment.GetEnvironmentVariable(key) is null)
                    Environment.SetEnvironmentVariable(key, value);
            }
            return;
        }
    }
}
