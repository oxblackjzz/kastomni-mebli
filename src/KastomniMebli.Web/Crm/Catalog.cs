namespace KastomniMebli.Web.Crm;

/// <summary>Довідники: ключ у базі → підпис українською.</summary>
public static class Catalog
{
    public static readonly IReadOnlyList<(string Key, string Label)> Sources =
    [
        ("site", "Сайт"),
        ("brother", "Брат"),
        ("referral", "Рекомендація"),
        ("furniture_maker", "Сторонній мебляр"),
        ("other", "Інше"),
    ];

    public static readonly IReadOnlyList<(string Key, string Label)> PaymentKinds =
    [
        ("advance", "Аванс"),
        ("extra", "Доплата"),
        ("refund", "Повернення"),
    ];

    public static readonly IReadOnlyList<(string Key, string Label)> PaymentMethods =
    [
        ("cash", "Готівка"),
        ("card", "Картка"),
        ("bank", "Рахунок"),
        ("other", "Інше"),
    ];

    public static readonly IReadOnlyList<(string Key, string Label)> ExpenseCategories =
    [
        ("board", "Плита"),
        ("edge", "Кромка"),
        ("hardware", "Фурнітура"),
        ("cutting", "Порізка"),
        ("delivery", "Доставка"),
        ("other", "Інше"),
    ];

    /// <summary>Що вважається «матеріалом» для часток у % від матеріалу, якщо в налаштуваннях не задано.</summary>
    public static readonly IReadOnlyList<string> DefaultMaterialCategories = ["board", "edge", "hardware"];

    public static readonly IReadOnlyList<(string Key, string Label)> ShareBases =
    [
        (ShareBasis.ContractPercent, "% від договору"),
        (ShareBasis.MaterialsPercent, "% від матеріалу"),
        (ShareBasis.Fixed, "Фіксована сума"),
    ];

    public static readonly IReadOnlyList<(string Key, string Label)> AssigneeRoles =
    [
        ("measurer", "Замірник"),
        ("designer", "Конструктор"),
        ("installer", "Монтажник"),
    ];

    public static readonly IReadOnlyList<(string Key, string Label)> FileKinds =
    [
        ("drawing", "Креслення"),
        ("measure_photo", "Фото заміру"),
        ("brief", "Від замовника"),
        ("other", "Інше"),
    ];

    public static string Label(IReadOnlyList<(string Key, string Label)> list, string? key) =>
        list.FirstOrDefault(x => x.Key == key).Label ?? key ?? "";

    public static bool Has(IReadOnlyList<(string Key, string Label)> list, string? key) =>
        list.Any(x => x.Key == key);
}

public static class ShareBasis
{
    public const string ContractPercent = "contract_percent";
    public const string MaterialsPercent = "materials_percent";
    public const string Fixed = "fixed";
}

public static class OrderStatuses
{
    public const string New = "new";
    public const string MeasureScheduled = "measure_scheduled";
    public const string Measured = "measured";
    public const string Drawing = "drawing";
    public const string Approved = "approved";
    public const string Production = "production";
    public const string Installation = "installation";
    public const string Done = "done";
    public const string Cancelled = "cancelled";

    /// <summary>Основний шлях замовлення, по порядку.</summary>
    public static readonly IReadOnlyList<(string Key, string Label)> Flow =
    [
        (New, "Нова заявка"),
        (MeasureScheduled, "Замір призначено"),
        (Measured, "Замір зроблено"),
        (Drawing, "Креслення"),
        (Approved, "Погоджено (аванс)"),
        (Production, "У виробництві"),
        (Installation, "Монтаж"),
        (Done, "Завершено"),
    ];

    public static readonly IReadOnlyList<(string Key, string Label)> All = [.. Flow, (Cancelled, "Скасовано")];

    public static string Label(string key) => Catalog.Label(All, key);

    public static bool IsKnown(string key) => Catalog.Has(All, key);

    /// <summary>Позиція в основному шляху; для «Скасовано» — -1.</summary>
    public static int Rank(string key)
    {
        for (var i = 0; i < Flow.Count; i++)
            if (Flow[i].Key == key)
                return i;
        return -1;
    }

    public static string? Next(string key)
    {
        var rank = Rank(key);
        return rank >= 0 && rank < Flow.Count - 1 ? Flow[rank + 1].Key : null;
    }

    public static bool IsOpen(string key) => key is not (Done or Cancelled);
}
