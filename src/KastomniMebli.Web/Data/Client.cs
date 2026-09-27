namespace KastomniMebli.Web.Data;

public class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>+380XXXXXXXXX; унікальний — за ним заявка з сайту знаходить наявного клієнта.</summary>
    public string? Phone { get; set; }

    public string? Address { get; set; }

    /// <summary>Telegram (без @) — у меблярів це часто основний канал зв'язку.</summary>
    public string? Telegram { get; set; }

    /// <summary>Див. <see cref="Crm.Catalog.Sources"/>.</summary>
    public string Source { get; set; } = "";

    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<Order> Orders { get; set; } = [];
}
