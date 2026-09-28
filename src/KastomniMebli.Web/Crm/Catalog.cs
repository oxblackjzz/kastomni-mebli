using KastomniMebli.Web.Data;

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

    public static readonly IReadOnlyList<(string Key, string Label)> CancelReasons =
    [
        ("expensive", "Дорого"),
        ("competitor", "Пішли до інших"),
        ("no_answer", "Не відповідає"),
        ("postponed", "Відклали"),
        ("not_our", "Не наш профіль"),
        ("spam", "Спам / помилкова заявка"),
        ("other", "Інше"),
    ];

    /// <summary>Загальні витрати (не на конкретне замовлення).</summary>
    public static readonly IReadOnlyList<(string Key, string Label)> CompanyExpenseCategories =
    [
        (AdvertisingCategory, "Реклама"),
        ("tools", "Інструмент"),
        ("fuel", "Пальне / дорога"),
        ("rent", "Оренда"),
        ("services", "Сервіси, зв'язок"),
        ("other", "Інше"),
    ];

    public const string AdvertisingCategory = "ads";

    public static string Label(IReadOnlyList<(string Key, string Label)> list, string? key) =>
        list.FirstOrDefault(x => x.Key == key).Label ?? key ?? "";

    public static bool Has(IReadOnlyList<(string Key, string Label)> list, string? key) =>
        list.Any(x => x.Key == key);
}

public static class AuditActions
{
    public const string PaymentAdded = "payment_added";
    public const string PaymentDeleted = "payment_deleted";
    public const string ExpenseAdded = "expense_added";
    public const string ExpenseDeleted = "expense_deleted";
    public const string ShareAdded = "share_added";
    public const string ShareChanged = "share_changed";
    public const string ShareDeleted = "share_deleted";
    public const string SharePaid = "share_paid";
    public const string ShareUnpaid = "share_unpaid";
    public const string TemplateApplied = "template_applied";
    public const string ContractChanged = "contract_changed";
    public const string Cancelled = "cancelled";
    public const string CompanyExpenseAdded = "company_exp_added";
    public const string CompanyExpenseDeleted = "company_exp_deleted";

    public static readonly IReadOnlyList<(string Key, string Label)> All =
    [
        (PaymentAdded, "Оплату додано"),
        (PaymentDeleted, "Оплату видалено"),
        (ExpenseAdded, "Витрату додано"),
        (ExpenseDeleted, "Витрату видалено"),
        (ShareAdded, "Частку додано"),
        (ShareChanged, "Частку змінено"),
        (ShareDeleted, "Частку видалено"),
        (SharePaid, "Частку виплачено"),
        (ShareUnpaid, "Виплату скасовано"),
        (TemplateApplied, "Частки за шаблоном"),
        (ContractChanged, "Сума договору"),
        (Cancelled, "Замовлення скасовано"),
        (CompanyExpenseAdded, "Загальну витрату додано"),
        (CompanyExpenseDeleted, "Загальну витрату видалено"),
    ];

    public static string Label(string key) => Catalog.Label(All, key);

    /// <summary>Дії, що зменшують гроші або скасовують — підсвічуються в журналі.</summary>
    public static bool IsRemoval(string key) =>
        key is PaymentDeleted or ExpenseDeleted or ShareDeleted or ShareUnpaid or CompanyExpenseDeleted;
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

    // B2B — креслення для стороннього мебляра: простіший цикл.
    public const string InWork = "in_work";
    public const string Review = "review";
    public const string Delivered = "delivered";
    public const string Paid = "paid";

    /// <summary>Основний шлях замовлення меблів, по порядку.</summary>
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

    public static readonly IReadOnlyList<(string Key, string Label)> B2bFlow =
    [
        (New, "Нова"),
        (InWork, "В роботі"),
        (Review, "На погодженні"),
        (Delivered, "Здано"),
        (Paid, "Оплачено"),
    ];

    public static readonly IReadOnlyList<(string Key, string Label)> All = [.. Flow, (Cancelled, "Скасовано")];

    public static IReadOnlyList<(string Key, string Label)> FlowFor(string kind) =>
        kind == OrderKinds.B2b ? B2bFlow : Flow;

    public static IReadOnlyList<(string Key, string Label)> AllFor(string kind) =>
        [.. FlowFor(kind), (Cancelled, "Скасовано")];

    /// <summary>Кінцевий успішний статус: «Завершено» для меблів, «Оплачено» для B2B.</summary>
    public static string FinalFor(string kind) => kind == OrderKinds.B2b ? Paid : Done;

    public static string Label(string key) => Catalog.Label(All, key);

    public static string Label(string kind, string key) => Catalog.Label(AllFor(kind), key);

    public static bool IsKnown(string key) => Catalog.Has(All, key) || Catalog.Has(B2bFlow, key);

    public static bool IsKnown(string kind, string key) => Catalog.Has(AllFor(kind), key);

    /// <summary>Позиція в основному шляху меблів; для «Скасовано» та B2B-статусів — -1.</summary>
    public static int Rank(string key) => Rank(OrderKinds.Retail, key);

    public static int Rank(string kind, string key)
    {
        var flow = FlowFor(kind);
        for (var i = 0; i < flow.Count; i++)
            if (flow[i].Key == key)
                return i;
        return -1;
    }

    public static string? Next(string key) => Next(OrderKinds.Retail, key);

    public static string? Next(string kind, string key)
    {
        var flow = FlowFor(kind);
        var rank = Rank(kind, key);
        return rank >= 0 && rank < flow.Count - 1 ? flow[rank + 1].Key : null;
    }

    public static bool IsOpen(string key) => key is not (Done or Paid or Cancelled);
}
