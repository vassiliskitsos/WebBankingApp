using System.Net;
using System.Net.Http.Json;
using Microsoft.Playwright;
using WebBanking.Contracts.Auth;

namespace WebBanking.Tests.Infrastructure;

// shared steps for the browser tests that sign in and out through the real UI
public static class E2EAuthHelpers
{
    public const string Password = "123456";
    public const string AuthSessionKey = "webbanking.auth.session";

    // WASM start-up is slow on a cold run, so UI waits use a generous timeout
    public const float UiTimeout = 30_000;

    public static async Task<string> RegisterUserAsync(E2EFixture fixture)
    {
        var username = $"e2e{Guid.NewGuid():N}"[..20];

        using var http = new HttpClient { BaseAddress = new Uri(fixture.ApiBaseUrl) };
        var register = await http.PostAsJsonAsync("api/auth/register",
            new RegisterRequest { Username = username, Password = Password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        return username;
    }

    // fills and submits the login form; does not wait for the outcome
    public static async Task SubmitLoginFormAsync(IPage page, E2EFixture fixture, string username, string password)
    {
        await page.GotoAsync($"{fixture.ApiBaseUrl}/login");
        // the form only submits through Blazor, so wait until the WASM app has rendered it
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Σύνδεση" }))
            .ToBeVisibleAsync(new() { Timeout = UiTimeout });
        await page.FillAsync("#username", username);
        await page.FillAsync("#password", password);
        await page.ClickAsync("button[type=submit]");
    }

    public static async Task SignInAsync(IPage page, E2EFixture fixture, string username)
    {
        await SubmitLoginFormAsync(page, fixture, username, Password);

        // IncludeHidden: the nav is collapsed on mobile viewports, so the button is display:none
        await Assertions.Expect(LogoutButton(page, includeHidden: true))
            .ToBeAttachedAsync(new() { Timeout = UiTimeout });
    }

    public static async Task SignOutAsync(IPage page)
    {
        await LogoutButton(page).ClickAsync();
        await Assertions.Expect(NavLoginLink(page)).ToBeVisibleAsync(new() { Timeout = UiTimeout });
        await page.WaitForURLAsync("**/login", new() { Timeout = UiTimeout });
    }

    public static Task<string?> ReadStoredSessionAsync(IPage page) =>
        page.EvaluateAsync<string?>("key => sessionStorage.getItem(key)", AuthSessionKey);

    public static ILocator LogoutButton(IPage page, bool includeHidden = false) =>
        page.GetByRole(AriaRole.Button, new() { Name = "Αποσύνδεση", IncludeHidden = includeHidden });

    public static ILocator NavLoginLink(IPage page) =>
        page.GetByRole(AriaRole.Navigation).GetByRole(AriaRole.Link, new() { Name = "Σύνδεση", Exact = true });
}
