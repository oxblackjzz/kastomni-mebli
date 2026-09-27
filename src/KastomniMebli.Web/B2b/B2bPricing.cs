namespace KastomniMebli.Web.B2b;

/// <summary>
/// Ціни на креслення для меблярів — задаються в CRM (Налаштування), у коді жодних цифр.
/// Порожнє поле — на сайті цей рядок не показується.
/// </summary>
public sealed class B2bPricing
{
    /// <summary>Відсоток від вартості матеріалу.</summary>
    public decimal? MaterialsPercent { get; set; }

    /// <summary>Мінімальний чек, грн.</summary>
    public decimal? MinimumCheck { get; set; }

    /// <summary>Доплата за вписування кухні під техніку й комунікації, грн.</summary>
    public decimal? KitchenFitSurcharge { get; set; }

    /// <summary>Фіксовані ціни за тип виробу.</summary>
    public List<B2bFixedPrice> FixedPrices { get; set; } = [];

    /// <summary>Довільне уточнення під цінами.</summary>
    public string? Note { get; set; }

    public bool IsEmpty =>
        MaterialsPercent is null && MinimumCheck is null && KitchenFitSurcharge is null &&
        FixedPrices.Count == 0 && string.IsNullOrWhiteSpace(Note);
}

public sealed class B2bFixedPrice
{
    public string Item { get; set; } = "";
    public decimal Price { get; set; }
}
