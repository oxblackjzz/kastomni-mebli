using Npgsql;

namespace KastomniMebli.Web.Data;

public static class DatabaseUrl
{
    /// <summary>
    /// Render дає DATABASE_URL у форматі postgres://user:pass@host:port/db — Npgsql такого не розуміє.
    /// Рядок у форматі Npgsql (Host=...;) повертається без змін.
    /// </summary>
    public static string ToNpgsql(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var user = uri.UserInfo.Split(':', 2);
        var b = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(user[0]),
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            // Внутрішня мережа Render — без SSL, зовнішній доступ — з SSL; Prefer покриває обидва.
            SslMode = SslMode.Prefer,
        };
        if (user.Length > 1)
            b.Password = Uri.UnescapeDataString(user[1]);

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<SslMode>(kv[1].Replace("-", ""), ignoreCase: true, out var mode))
                b.SslMode = mode;
        }

        return b.ConnectionString;
    }

    public static string? Resolve(IConfiguration config)
    {
        var value = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(value))
            value = config["DATABASE_URL"];
        return string.IsNullOrWhiteSpace(value) ? null : ToNpgsql(value);
    }
}
