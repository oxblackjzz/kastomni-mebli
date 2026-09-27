namespace KastomniMebli.Web.Data;

/// <summary>Зроблена робота для портфоліо: назва, тип, фото. Показується на /roboty і (вибрані) на головній.</summary>
public class PortfolioWork
{
    public int Id { get; set; }
    public string Title { get; set; } = "";

    /// <summary>Ключ із <see cref="Leads.FurnitureTypes"/>.</summary>
    public string Category { get; set; } = "";

    /// <summary>Місто / район, напр. «Звягель». Необов'язково.</summary>
    public string? Location { get; set; }

    public string? Description { get; set; }

    /// <summary>Чернетка не видна на сайті.</summary>
    public bool IsPublished { get; set; }

    /// <summary>Показувати в блоці на головній (там 6 місць).</summary>
    public bool OnHome { get; set; }

    /// <summary>Показувати як приклад на сторінці для меблярів (фрагменти креслень).</summary>
    public bool ForMakers { get; set; }

    /// <summary>Менше — вище.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<PortfolioPhoto> Photos { get; set; } = [];
}

public class PortfolioPhoto
{
    public int Id { get; set; }
    public int WorkId { get; set; }

    /// <summary>Велике фото (до 1920 px) — шлях відносно сховища файлів.</summary>
    public string LargePath { get; set; } = "";

    /// <summary>Мініатюра (до 720 px).</summary>
    public string ThumbPath { get; set; } = "";

    public string? Caption { get; set; }

    /// <summary>Перше за порядком — обкладинка.</summary>
    public int SortOrder { get; set; }

    public DateTime UploadedAt { get; set; }
}
