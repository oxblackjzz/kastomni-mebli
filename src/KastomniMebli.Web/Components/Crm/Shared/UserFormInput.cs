using KastomniMebli.Web.Crm.Auth;

namespace KastomniMebli.Web.Components.Crm.Shared;

public sealed class UserFormInput
{
    public string Login { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = Roles.Installer;
    public string? Phone { get; set; }
    public string? TelegramChatId { get; set; }

    public UserInput ToInput() => new(Login, DisplayName, Role, Phone, TelegramChatId);
}
