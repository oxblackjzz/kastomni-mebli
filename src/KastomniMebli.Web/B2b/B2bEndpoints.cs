using System.Text;
using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Leads;
using KastomniMebli.Web.Notifications;
using KastomniMebli.Web.Settings;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KastomniMebli.Web.B2b;

public static class B2bEndpoints
{
    public const string PagePath = "/dlya-meblyariv";
    public const string SubmitPath = "/dlya-meblyariv/zavdannia";

    public static void MapB2bEndpoints(this IEndpointRouteBuilder app)
    {
        // Форму читаємо вручну: потрібно підняти ліміт розміру запиту до читання тіла (файли).
        app.MapPost(SubmitPath, SubmitAsync).RequireRateLimiting(LeadEndpoints.RateLimitPolicy);
    }

    private static async Task<IResult> SubmitAsync(
        HttpContext http,
        OrderService orders,
        FileService files,
        FormTiming timing,
        TimeProvider time,
        B2bNotifier notifier,
        IOptions<SiteSettings> site,
        ILoggerFactory loggers)
    {
        var log = loggers.CreateLogger(typeof(B2bEndpoints));
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = B2bForm.MaxTotalBytes + 5 * 1024 * 1024;
        if (!http.Request.HasFormContentType)
            return Results.BadRequest();

        B2bForm input;
        try
        {
            input = B2bForm.From(await http.Request.ReadFormAsync());
        }
        catch (Exception ex) when (ex is InvalidDataException or BadHttpRequestException or IOException)
        {
            log.LogWarning(ex, "Не вдалося прочитати форму B2B");
            return Invalid(http, new Dictionary<string, string> { ["files"] = "Файли завеликі. Надішліть їх у Telegram." },
                "Не вдалося прийняти файли.");
        }

        if (input.IsBot)
        {
            log.LogInformation("B2B-заявку відкинуто: заповнене поле-пастка");
            return Success(http, 0);
        }
        if (timing.IsTooFast(input.TimingToken))
            return Invalid(http, new Dictionary<string, string>(), "Форму надіслано надто швидко. Перевірте дані й натисніть ще раз.");

        var result = input.Validate(Kyiv.Today(time), files.MaxBytes);
        if (!result.IsValid)
            return Invalid(http, result.Errors, "Перевірте поля, позначені червоним.");
        var req = result.Request!;

        Order order;
        try
        {
            order = await orders.CreateB2bFromFormAsync(req.Name, req.Phone, req.Telegram, req.Types, req.Due, req.Comment);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Не вдалося зберегти B2B-заявку");
            return Failure(http, site.Value);
        }

        var failed = new List<string>();
        foreach (var file in req.Files)
        {
            try
            {
                await using var stream = file.OpenReadStream();
                await files.SaveFromPublicFormAsync(order.Id, file.FileName, stream, file.Length);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "B2B-заявка {Number}: не збережено файл {File}", order.Number, file.FileName);
                failed.Add(file.FileName);
            }
        }

        await notifier.NewRequestAsync(order, req, failed);
        log.LogInformation("Нова B2B-заявка {Number}", order.Number);
        return Success(http, failed.Count);
    }

    public static string SuccessMessage(int failedFiles) =>
        failedFiles == 0
            ? "Дякую! Завдання отримав. Перегляну й напишу вам із ціною та терміном."
            : "Завдання отримав, але частину файлів не вдалося зберегти. Надішліть їх, будь ласка, у Telegram.";

    private static IResult Success(HttpContext http, int failedFiles) =>
        LeadEndpoints.WantsJson(http)
            ? Results.Json(new { ok = true, message = SuccessMessage(failedFiles) })
            : Results.Redirect($"{PagePath}?zayavka=ok#zavdannia");

    private static IResult Invalid(HttpContext http, IReadOnlyDictionary<string, string> errors, string message) =>
        LeadEndpoints.WantsJson(http)
            ? Results.Json(new { ok = false, message, errors }, statusCode: StatusCodes.Status400BadRequest)
            : Results.Redirect($"{PagePath}?zayavka=invalid#zavdannia");

    private static IResult Failure(HttpContext http, SiteSettings site) =>
        LeadEndpoints.WantsJson(http)
            ? Results.Json(new { ok = false, message = LeadEndpoints.FailureMessage(site) }, statusCode: StatusCodes.Status500InternalServerError)
            : Results.Redirect($"{PagePath}?zayavka=error#zavdannia");
}

/// <summary>Нова B2B-заявка → особисто адміністраторам (конструктору), інакше в загальний чат.</summary>
public sealed class B2bNotifier(ITelegramSender telegram, IDbContextFactory<AppDbContext> dbs, IOptions<SiteSettings> site, ILogger<B2bNotifier> log)
{
    public async Task NewRequestAsync(Order order, ValidB2b req, IReadOnlyList<string> failedFiles)
    {
        var sb = new StringBuilder();
        sb.Append("<b>Нове замовлення креслення (B2B) ").Append(order.Number).Append("</b>\n\n");
        TelegramMessage.Line(sb, "Цех / ім'я", req.Name);
        TelegramMessage.Line(sb, "Телефон", PhoneNumber.Format(req.Phone));
        TelegramMessage.Line(sb, "Telegram", req.Telegram is null ? null : "@" + req.Telegram);
        TelegramMessage.Line(sb, "Що", req.Types.Count > 0 ? string.Join(", ", req.Types.Select(FurnitureTypes.Label)) : null);
        TelegramMessage.Line(sb, "Термін", req.Due is { } d ? Kyiv.Format(d) : null);
        TelegramMessage.Line(sb, "Файлів", (req.Files.Count - failedFiles.Count).ToString());
        if (failedFiles.Count > 0)
            TelegramMessage.Line(sb, "Не збережено", string.Join(", ", failedFiles));
        TelegramMessage.Line(sb, "Коментар", req.Comment);
        var text = sb.ToString().TrimEnd();
        if (TelegramMessage.OrderLink(site.Value, order.Id) is { } link)
            text += "\n\n" + link;

        List<string> chats;
        await using (var db = await dbs.CreateDbContextAsync())
        {
            chats = await db.Users.Where(u => u.IsActive && u.Role == Roles.Admin && u.TelegramChatId != null)
                .Select(u => u.TelegramChatId!).ToListAsync();
        }
        if (chats.Count == 0)
            chats = [.. telegram.TeamChatIds];

        foreach (var chat in chats)
        {
            try
            {
                await telegram.SendAsync(chat, text, CancellationToken.None);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Не вдалося надіслати B2B-заявку {Number} у Telegram", order.Number);
            }
        }
    }
}
