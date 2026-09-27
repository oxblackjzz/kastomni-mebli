using KastomniMebli.Web.Settings;
using Microsoft.Extensions.Options;

namespace KastomniMebli.Web.Leads;

public static class LeadEndpoints
{
    public const string Path = "/zayavka";
    public const string RateLimitPolicy = "leads";

    public static void MapLeadEndpoints(this IEndpointRouteBuilder app)
    {
        // Антифорджері вимкнено свідомо: форма публічна й анонімна, красти тут нічого.
        app.MapPost(Path, SubmitAsync)
            .DisableAntiforgery()
            .RequireRateLimiting(RateLimitPolicy);
    }

    private static async Task<IResult> SubmitAsync(
        HttpContext http,
        IFormCollection form,
        LeadService leads,
        FormTiming timing,
        ClientIp clientIp,
        IOptions<SiteSettings> site,
        ILoggerFactory loggers)
    {
        var log = loggers.CreateLogger(typeof(LeadEndpoints));
        var input = LeadForm.From(form);

        if (input.IsBot)
        {
            // Бот бачить «успіх» і не пробує ще раз, але нічого не зберігаємо.
            log.LogInformation("Заявку відкинуто: заповнене поле-пастка");
            return Success(http, input.Name?.Trim() ?? "");
        }

        if (timing.IsTooFast(input.TimingToken))
        {
            log.LogInformation("Заявку відкинуто: форму надіслано надто швидко");
            return Invalid(http, new Dictionary<string, string>(),
                "Форму надіслано надто швидко. Перевірте дані й натисніть «Надіслати» ще раз.");
        }

        var result = input.Validate();
        if (!result.IsValid)
            return Invalid(http, result.Errors, "Перевірте поля, позначені червоним.");

        try
        {
            var lead = await leads.SubmitAsync(result.Lead!, clientIp.Get(http), http.Request.Headers.UserAgent.ToString());
            return Success(http, lead.Name);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Не вдалося зберегти заявку");
            return Failure(http, site.Value);
        }
    }

    public static string SuccessMessage(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? "Дякуємо! Заявку отримали. Зателефонуємо, щоб домовитись про замір."
            : $"Дякуємо, {name}! Заявку отримали. Зателефонуємо, щоб домовитись про замір.";

    public static string FailureMessage(SiteSettings site) =>
        string.IsNullOrWhiteSpace(site.Phone)
            ? "Не вдалося надіслати заявку. Спробуйте ще раз трохи згодом."
            : $"Не вдалося надіслати заявку. Зателефонуйте нам: {site.Phone}";

    public static string RateLimitedMessage(SiteSettings site) =>
        string.IsNullOrWhiteSpace(site.Phone)
            ? "Забагато заявок за короткий час. Спробуйте пізніше."
            : $"Забагато заявок за короткий час. Зателефонуйте нам: {site.Phone}";

    /// <summary>JS-форма просить JSON; без JS браузер отримує редирект назад на сторінку.</summary>
    public static bool WantsJson(HttpContext http) =>
        http.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

    private static IResult Success(HttpContext http, string name) =>
        WantsJson(http)
            ? Results.Json(new { ok = true, message = SuccessMessage(name) })
            : Results.Redirect("/?zayavka=ok#zayavka");

    private static IResult Invalid(HttpContext http, IReadOnlyDictionary<string, string> errors, string message) =>
        WantsJson(http)
            ? Results.Json(new { ok = false, message, errors }, statusCode: StatusCodes.Status400BadRequest)
            : Results.Redirect("/?zayavka=invalid#zayavka");

    private static IResult Failure(HttpContext http, SiteSettings site) =>
        WantsJson(http)
            ? Results.Json(new { ok = false, message = FailureMessage(site) }, statusCode: StatusCodes.Status500InternalServerError)
            : Results.Redirect("/?zayavka=error#zayavka");
}
