using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace KastomniMebli.Web.Leads;

public sealed class ProxyOptions
{
    public const string Section = "Proxy";

    /// <summary>
    /// true — застосунок стоїть за проксі (Render + Cloudflare), і заголовкам з IP клієнта можна вірити.
    /// Локально — false.
    /// </summary>
    public bool TrustForwardedHeaders { get; set; }
}

public sealed class ClientIp(IOptions<ProxyOptions> options)
{
    public string? Get(HttpContext http)
    {
        if (options.Value.TrustForwardedHeaders)
        {
            // Cloudflare сам виставляє цей заголовок, підробити його клієнт не може.
            if (TryParse(http.Request.Headers["CF-Connecting-IP"], out var cf))
                return cf;
            // Перший у ланцюжку — адреса клієнта. Його можна підробити, але це лише дозволить
            // обійти ліміт частоти; помилитися в інший бік (усі клієнти як одна IP) гірше.
            var xff = http.Request.Headers["X-Forwarded-For"].ToString();
            if (TryParse(xff.Split(',')[0], out var first))
                return first;
        }
        return http.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>Короткий хеш для таблиці заявок: видно повтори, але не саму адресу.</summary>
    public static string? Hash(string? ip)
    {
        if (ip is null)
            return null;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("kastomni-mebli:" + ip));
        return Convert.ToHexStringLower(bytes)[..16];
    }

    private static bool TryParse(string? value, out string ip)
    {
        ip = "";
        if (!IPAddress.TryParse(value?.Trim(), out var address))
            return false;
        ip = address.ToString();
        return true;
    }
}
