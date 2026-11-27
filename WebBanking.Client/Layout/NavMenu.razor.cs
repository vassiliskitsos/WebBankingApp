using Microsoft.AspNetCore.Components;
using WebBanking.Client.Services;

namespace WebBanking.Client.Layout;

public partial class NavMenu
{
    private bool collapseNavMenu = true;

    private string? NavMenuCssClass => collapseNavMenu ? "collapse" : null;

    [Inject]
    private JwtAuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private void ToggleNavMenu()
    {
        collapseNavMenu = !collapseNavMenu;
    }

    private async Task SignOutAsync()
    {
        collapseNavMenu = true;
        await AuthStateProvider.SignOutAsync();
        Navigation.NavigateTo("login");
    }
}
