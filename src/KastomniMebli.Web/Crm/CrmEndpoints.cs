using KastomniMebli.Web.Crm.Auth;
using Microsoft.Net.Http.Headers;

namespace KastomniMebli.Web.Crm;

public static class CrmEndpoints
{
    public static void MapCrmEndpoints(this IEndpointRouteBuilder app)
    {
        // Креслення й фото: PDF і картинки відкриваються в браузері, решта (DWG) — завантажуються.
        app.MapGet("/crm/fajly/{id:int}", async (int id, HttpContext http, FileService files) =>
        {
            var actor = CurrentUser.From(http.User);
            if (actor is null || await files.OpenAsync(actor, id) is not { } found)
                return Results.NotFound();

            var inline = found.File.ContentType.StartsWith("image/") || found.File.ContentType == "application/pdf";
            var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
            disposition.SetHttpFileName(found.File.FileName);
            http.Response.Headers.ContentDisposition = disposition.ToString();
            http.Response.Headers.CacheControl = "private, max-age=3600";
            return Results.File(found.Path, found.File.ContentType, enableRangeProcessing: true);
        }).RequireAuthorization();

        // Вивантаження місяця для бухгалтерії (CSV для Excel).
        app.MapGet("/crm/eksport/{year:int}/{month:int}/{kind}.csv", async (int year, int month, string kind, HttpContext http, ExportService export) =>
        {
            if (CurrentUser.From(http.User) is not { CanSeeMoney: true } actor || month is < 1 or > 12 || year is < 2020 or > 2100
                || !ExportService.Kinds.Contains(kind))
                return Results.NotFound();
            var bytes = await export.ExportAsync(actor, kind, year, month);
            return Results.File(bytes, "text/csv; charset=utf-8", $"{kind}-{year}-{month:00}.csv");
        }).RequireAuthorization();

        // Фото постів — публічно за випадковим ключем: Instagram і Facebook забирають їх за URL без входу.
        app.MapGet(Posting.PostMedia.PublicPrefix + "{key:regex(^[0-9a-f]{{32}}$)}.jpg", async (string key, HttpContext http, Posting.PostService posts) =>
        {
            if (await posts.PublicPhotoPathAsync(key) is not { } path)
                return Results.NotFound();
            http.Response.Headers.CacheControl = "public, max-age=86400";
            return Results.File(path, "image/jpeg");
        });

        // Фото портфоліо для сайту: t — мініатюра, l — велике. Чернетки — лише для тих, хто в CRM.
        app.MapGet("/roboty/foto/{id:int}/{size:regex(^[tl]$)}", async (int id, string size, HttpContext http, PortfolioService portfolio) =>
        {
            var editor = CurrentUser.From(http.User)?.CanSeeMoney == true;
            var path = await portfolio.PhotoPathAsync(id, thumb: size == "t", includeDrafts: editor);
            if (path is null)
                return Results.NotFound();
            // Шлях файлу незмінний (новий файл — новий id), тож кешуємо надовго.
            http.Response.Headers.CacheControl = editor ? "private, max-age=300" : "public, max-age=2592000, immutable";
            return Results.File(path, "image/jpeg");
        });
    }
}
