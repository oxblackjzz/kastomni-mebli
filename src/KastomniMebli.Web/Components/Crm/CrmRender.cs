using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace KastomniMebli.Web.Components.Crm;

public static class CrmRender
{
    /// <summary>Сторінки CRM — інтерактивні, без пререндеру (дані вантажаться один раз, після підключення).</summary>
    public static IComponentRenderMode Mode { get; } = new InteractiveServerRenderMode(prerender: false);
}
