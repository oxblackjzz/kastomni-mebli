using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Crm.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace KastomniMebli.Web.Components.Crm;

/// <summary>Спільне для сторінок CRM: поточний користувач, завантаження, показ помилок.</summary>
public abstract class CrmPageBase : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState>? AuthState { get; set; }

    [Inject]
    protected ILogger<CrmPageBase> Log { get; set; } = null!;

    protected CurrentUser Me { get; private set; } = null!;
    protected bool Loading { get; private set; } = true;
    protected string? Error { get; set; }
    protected string? Notice { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var state = AuthState is null ? null : await AuthState;
        Me = CurrentUser.From(state?.User) ?? throw new InvalidOperationException("Немає користувача");
        await Run(LoadAsync);
        Loading = false;
    }

    protected virtual Task LoadAsync() => Task.CompletedTask;

    /// <summary>Виконати дію; помилку показати зверху сторінки, а не «впасти».</summary>
    protected async Task<bool> Run(Func<Task> action, string? notice = null)
    {
        Error = null;
        Notice = null;
        try
        {
            await action();
            Notice = notice;
            return true;
        }
        catch (CrmException ex)
        {
            Error = ex.Message;
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Помилка на сторінці CRM");
            Error = "Щось пішло не так. Оновіть сторінку й спробуйте ще раз.";
        }
        return false;
    }
}
