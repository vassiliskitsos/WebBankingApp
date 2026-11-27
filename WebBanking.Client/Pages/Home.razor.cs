using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace WebBanking.Client.Pages;

public partial class Home : IDisposable
{
    private string? _userName;

    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    [Inject]
    private ILogger<Home> Logger { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        AuthStateProvider.AuthenticationStateChanged += OnAuthenticationStateChangedAsync;
        await ReadUserNameAsync();
    }

    private async void OnAuthenticationStateChangedAsync(Task<AuthenticationState> stateTask)
    {
        try
        {
            await ReadUserNameAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to refresh the home page after an authentication state change.");
        }
    }

    private async Task ReadUserNameAsync()
    {
        var state = await AuthStateProvider.GetAuthenticationStateAsync();
        _userName = state.User.Identity?.IsAuthenticated == true ? state.User.Identity.Name : null;
    }

    public void Dispose()
    {
        AuthStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChangedAsync;
    }
}
