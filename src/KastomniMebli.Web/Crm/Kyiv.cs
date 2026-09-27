using System.Globalization;

namespace KastomniMebli.Web.Crm;

/// <summary>У базі — UTC, людям — київський час.</summary>
public static class Kyiv
{
    public static TimeZoneInfo Zone { get; } = Find();

    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("uk-UA");

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);

    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(ToLocal(time.GetUtcNow().UtcDateTime));

    /// <summary>Межі місяця (київський час) в UTC: [from, to).</summary>
    public static (DateTime From, DateTime To) MonthUtc(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        return (ToUtc(start), ToUtc(start.AddMonths(1)));
    }

    public static string Format(DateTime? utc, string format = "dd.MM.yyyy HH:mm") =>
        utc is null ? "—" : ToLocal(utc.Value).ToString(format, Culture);

    public static string Format(DateOnly? date) =>
        date is null ? "—" : date.Value.ToString("dd.MM.yyyy", Culture);

    /// <summary>15 000 грн / 1 234,50 грн.</summary>
    public static string Money(decimal amount) =>
        (amount == Math.Truncate(amount) ? amount.ToString("#,0", Culture) : amount.ToString("#,0.00", Culture)) + " грн";

    private static TimeZoneInfo Find()
    {
        foreach (var id in new[] { "Europe/Kyiv", "Europe/Kiev", "FLE Standard Time" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz))
                return tz;
        }
        return TimeZoneInfo.Utc;
    }
}
