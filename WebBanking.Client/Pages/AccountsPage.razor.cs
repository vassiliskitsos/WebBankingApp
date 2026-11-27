using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using WebBanking.Client.Services.Interfaces;
using WebBanking.Contracts.Accounts;
using WebBanking.Contracts.Common;

namespace WebBanking.Client.Pages;

public partial class AccountsPage : IDisposable
{
    [Inject]
    private IApiClient ApiClient { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private ILogger<AccountsPage> Logger { get; set; } = default!;

    private IReadOnlyList<AccountDto>? accounts;
    private bool isLoading = true;
    private bool isUnauthorized;
    private bool isSignedIn;
    private string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        AuthStateProvider.AuthenticationStateChanged += OnAuthenticationStateChangedAsync;

        var state = await AuthStateProvider.GetAuthenticationStateAsync();
        isSignedIn = state.User.Identity?.IsAuthenticated == true;

        if (!isSignedIn)
        {
            isLoading = false;
            Navigation.NavigateTo("login");
            return;
        }

        await LoadAccountsAsync();
    }

    private async void OnAuthenticationStateChangedAsync(Task<AuthenticationState> stateTask)
    {
        try
        {
            var state = await stateTask;
            var wasSignedIn = isSignedIn;
            isSignedIn = state.User.Identity?.IsAuthenticated == true;

            if (!isSignedIn)
            {
                accounts = null;
                errorMessage = null;
                isLoading = false;
            }
            else if (!wasSignedIn)
            {
                isLoading = true;
                await InvokeAsync(StateHasChanged);
                await LoadAccountsAsync();
            }

            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to handle an authentication state change on the accounts page.");
            isLoading = false;
            errorMessage = "Δεν ήταν δυνατή η φόρτωση των λογαριασμών. Δοκιμάστε ξανά.";
            await InvokeAsync(StateHasChanged);
        }
    }

    public void Dispose()
    {
        AuthStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChangedAsync;
    }

    private async Task LoadAccountsAsync()
    {
        isLoading = true;
        errorMessage = null;
        isUnauthorized = false;

        var result = await ApiClient.GetAsync<IReadOnlyList<AccountDto>>("api/accounts");

        if (result.IsSuccess)
        {
            accounts = result.Value;
        }
        else
        {
            accounts = null;
            isUnauthorized = result.Error!.Type == ErrorType.Unauthorized;
            errorMessage = isUnauthorized
                ? "Η συνεδρία σας έχει λήξει. Παρακαλούμε συνδεθείτε ξανά."
                : result.Error.Message;
        }

        isLoading = false;
    }

    private static string FormatBalance(decimal balance) =>
        balance.ToString("N2", CultureInfo.InvariantCulture);
}
