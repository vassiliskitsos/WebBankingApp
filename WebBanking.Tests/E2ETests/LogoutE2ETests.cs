using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using WebBanking.Contracts.Auth;
using WebBanking.Tests.Infrastructure;

namespace WebBanking.Tests.E2ETests;

[Collection(E2ECollection.Name)]
public sealed class LogoutE2ETests
{
    private readonly E2EFixture _fixture;

    public LogoutE2ETests(E2EFixture fixture)
    {
        _fixture = fixture;
    }

    private const string Password = "123456";
    private const string AuthSessionKey = "webbanking.auth.session";

    [Fact]
    public async Task Logout_ClearsUiState_AndProtectedPageRedirectsToLogin()
    {
        var username = await RegisterUserAsync();

        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await SignInAsync(page, username);

        Assert.NotNull(await ReadStoredSessionAsync(page));

        await page.GetByRole(AriaRole.Button, new() { Name = "Αποσύνδεση" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Navigation).GetByRole(AriaRole.Link, new() { Name = "Σύνδεση", Exact = true }))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });
        Assert.Null(await ReadStoredSessionAsync(page));

        await page.GotoAsync($"{_fixture.ApiBaseUrl}/accounts");

        await page.WaitForURLAsync("**/login", new() { Timeout = 30_000 });
    }

    [Fact]
    public async Task Logout_OnMobileViewport_ClosesNavMenu()
    {
        var username = await RegisterUserAsync();

        await using var context = await _fixture.Browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = 375, Height = 812 }
        });
        var page = await context.NewPageAsync();
        await SignInAsync(page, username);

        var navMenu = page.Locator(".nav-scrollable");
        await page.Locator(".navbar-toggler").ClickAsync();
        await Assertions.Expect(navMenu).Not.ToHaveClassAsync(new Regex(@"\bcollapse\b"));

        await page.GetByRole(AriaRole.Button, new() { Name = "Αποσύνδεση" }).ClickAsync();

        await Assertions.Expect(navMenu).ToHaveClassAsync(new Regex(@"\bcollapse\b"), new() { Timeout = 10_000 });
    }

    private async Task<string> RegisterUserAsync()
    {
        var username = $"e2e{Guid.NewGuid():N}"[..20];

        using var http = new HttpClient { BaseAddress = new Uri(_fixture.ApiBaseUrl) };
        var register = await http.PostAsJsonAsync("api/auth/register",
            new RegisterRequest { Username = username, Password = Password });
        Assert.Equal(System.Net.HttpStatusCode.Created, register.StatusCode);

        return username;
    }

    private async Task SignInAsync(IPage page, string username)
    {
        await page.GotoAsync($"{_fixture.ApiBaseUrl}/login");
        // the form only submits through Blazor, so wait until the WASM app has rendered it
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Σύνδεση" }))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });
        await page.FillAsync("#username", username);
        await page.FillAsync("#password", Password);
        await page.ClickAsync("button[type=submit]");

        // IncludeHidden: the nav is collapsed on mobile viewports, so the button is display:none
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Αποσύνδεση", IncludeHidden = true }))
            .ToBeAttachedAsync(new() { Timeout = 30_000 });
    }

    private static Task<string?> ReadStoredSessionAsync(IPage page) =>
        page.EvaluateAsync<string?>("key => sessionStorage.getItem(key)", AuthSessionKey);
}
