using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace KastomniMebli.Web.Leads;

/// <summary>
/// Зашифрована мітка часу, коли сторінку з формою віддали браузеру.
/// Якщо форму надіслали швидше ніж за MinFillSeconds — це майже напевно бот.
/// Мітка без підпису або зі старим ключем (після редеплою) НЕ вважається спамом:
/// краще пропустити бота, ніж загубити справжню заявку.
/// </summary>
public sealed class FormTiming(IDataProtectionProvider dataProtection, TimeProvider time, IOptions<LeadOptions> options)
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("KastomniMebli.LeadForm.Timing");

    public string Issue() =>
        _protector.Protect(time.GetUtcNow().ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));

    public bool IsTooFast(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;
        try
        {
            var issued = long.Parse(_protector.Unprotect(token), CultureInfo.InvariantCulture);
            var elapsed = time.GetUtcNow().ToUnixTimeMilliseconds() - issued;
            return elapsed < options.Value.MinFillSeconds * 1000L;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
