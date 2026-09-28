namespace KastomniMebli.Web.Data;

/// <summary>
/// Журнал змін грошей: хто, коли й що додав, змінив чи видалив (оплати, витрати, частки, виплати, сума договору).
/// Лише додається — з CRM не редагується й не видаляється.
/// </summary>
public class AuditEntry
{
    public long Id { get; set; }
    public DateTime At { get; set; }
    public int? UserId { get; set; }
    public string UserName { get; set; } = "";
    public int? OrderId { get; set; }
    public string Action { get; set; } = "";
    public string Details { get; set; } = "";
    public decimal? Amount { get; set; }
}
