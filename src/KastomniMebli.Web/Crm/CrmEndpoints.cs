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
    }
}
