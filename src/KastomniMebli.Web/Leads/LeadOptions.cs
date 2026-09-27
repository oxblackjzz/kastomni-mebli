namespace KastomniMebli.Web.Leads;

public sealed class LeadOptions
{
    public const string Section = "Leads";

    /// <summary>Скільки заявок на годину приймаємо з однієї IP.</summary>
    public int RateLimitPerHour { get; set; } = 5;

    /// <summary>Швидше за це форму заповнити не можна — так поводяться боти.</summary>
    public int MinFillSeconds { get; set; } = 3;
}
