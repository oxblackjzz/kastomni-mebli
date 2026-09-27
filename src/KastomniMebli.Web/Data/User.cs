namespace KastomniMebli.Web.Data;

/// <summary>Користувач CRM: ти, брат, монтажник.</summary>
public class User
{
    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string DisplayName { get; set; } = "";

    /// <summary>Див. <see cref="Crm.Auth.Roles"/>.</summary>
    public string Role { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    /// <summary>Змінюється при зміні пароля/ролі — старі сесії на інших пристроях стають недійсними.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public string? Phone { get; set; }

    /// <summary>Особистий chat id у Telegram для сповіщень (монтаж, нагадування).</summary>
    public string? TelegramChatId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
