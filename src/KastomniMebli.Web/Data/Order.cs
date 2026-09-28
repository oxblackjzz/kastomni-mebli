namespace KastomniMebli.Web.Data;

public class Order
{
    public int Id { get; set; }

    /// <summary>Номер для людей: 2026-001.</summary>
    public string Number { get; set; } = "";

    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;

    /// <summary>Заявка з сайту, з якої створено замовлення.</summary>
    public long? LeadId { get; set; }

    /// <summary>retail — меблі клієнту; b2b — креслення для стороннього мебляра (етап 3).</summary>
    public string Kind { get; set; } = OrderKinds.Retail;

    public List<string> FurnitureTypes { get; set; } = [];
    public string Status { get; set; } = Crm.OrderStatuses.New;
    public string Source { get; set; } = "";

    /// <summary>Для заявок із сайту — канал (instagram, google, direct…) і рекламна кампанія.</summary>
    public string? Channel { get; set; }
    public string? Campaign { get; set; }

    public string? Address { get; set; }

    /// <summary>Сума договору, грн. Порожня, поки не домовились.</summary>
    public decimal? ContractAmount { get; set; }

    public string? Comment { get; set; }

    /// <summary>Бажаний термін (B2B-креслення).</summary>
    public DateOnly? DueDate { get; set; }

    // Заплановані зустрічі (UTC).
    public DateTime? MeasureDate { get; set; }
    public DateTime? InstallDate { get; set; }

    // Віхи (UTC): коли замовлення вперше дійшло до етапу. З них рахується конверсія.
    public DateTime? MeasuredAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<OrderAssignee> Assignees { get; set; } = [];
    public List<OrderStatusChange> StatusHistory { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    public List<Expense> Expenses { get; set; } = [];
    public List<OrderShare> Shares { get; set; } = [];
    public List<OrderFile> Files { get; set; } = [];
}

public static class OrderKinds
{
    public const string Retail = "retail";
    public const string B2b = "b2b";

    public static readonly IReadOnlyList<(string Key, string Label)> All =
    [
        (Retail, "Меблі клієнту"),
        (B2b, "Креслення для мебляра (B2B)"),
    ];
}

/// <summary>Хто відповідає за замовлення: замірник / конструктор / монтажник.</summary>
public class OrderAssignee
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Role { get; set; } = "";
}

public class OrderStatusChange
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = "";
    public int? ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; }
}

/// <summary>Гроші від клієнта. Сума завжди додатна; повернення — окремим видом.</summary>
public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Kind { get; set; } = "";
    public decimal Amount { get; set; }
    public DateOnly PaidOn { get; set; }
    public string Method { get; set; } = "";
    public string? Note { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Expense
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public DateOnly SpentOn { get; set; }
    public string? Note { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Частка учасника в конкретному замовленні. Копіюється з шаблону при створенні замовлення,
/// далі її можна змінити вручну. Сума рахується з бази й значення (див. OrderFinance).
/// </summary>
public class OrderShare
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Basis { get; set; } = "";
    public decimal Value { get; set; }

    /// <summary>Скільки фактично виплачено (фіксується в момент виплати). null — не виплачено.</summary>
    public decimal? PaidAmount { get; set; }
    public DateOnly? PaidOn { get; set; }
}

/// <summary>Шаблон часток за замовчуванням — налаштовується, не в коді.</summary>
public class ShareTemplate
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Basis { get; set; } = "";
    public decimal Value { get; set; }
    public bool IsActive { get; set; } = true;
}

public class OrderFile
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Kind { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public int? UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }

    /// <summary>Шлях відносно кореня сховища файлів (Render Disk), напр. orders/12/3f2a….pdf.</summary>
    public string StoragePath { get; set; } = "";
}

public class AppSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>Ключі шифрування cookie входу — в базі, щоб після редеплою не викидало з CRM.</summary>
public class DataProtectionKey
{
    public int Id { get; set; }
    public string FriendlyName { get; set; } = "";
    public string Xml { get; set; } = "";
}
