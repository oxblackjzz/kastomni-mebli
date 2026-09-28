namespace KastomniMebli.Web.Data;

/// <summary>Витрата, не прив'язана до замовлення: реклама, інструмент, пальне тощо.</summary>
public class CompanyExpense
{
    public int Id { get; set; }

    /// <summary>Ключ із <see cref="Crm.Catalog.CompanyExpenseCategories"/>.</summary>
    public string Category { get; set; } = "";

    public decimal Amount { get; set; }
    public DateOnly SpentOn { get; set; }
    public string? Note { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
