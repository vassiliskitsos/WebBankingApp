using System.Text.RegularExpressions;
using Microsoft.Playwright;
using WebBanking.Tests.Infrastructure;
using static WebBanking.Tests.Infrastructure.E2EAuthHelpers;

namespace WebBanking.Tests.E2ETests;

[Collection(E2ECollection.Name)]
public sealed class LogoutE2ETests
{
    private readonly E2EFixture _fixture;

    public LogoutE2ETests(E2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Logout_ClearsUiState_AndProtectedPageRedirectsToLogin()
    {
        var username = await RegisterUserAsync(_fixture);

        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await SignInAsync(page, _fixture, username);

        Assert.NotNull(await ReadStoredSessionAsync(page));

        await LogoutButton(page).ClickAsync();

        await Assertions.Expect(NavLoginLink(page)).ToBeVisibleAsync(new() { Timeout = UiTimeout });
        Assert.Null(await ReadStoredSessionAsync(page));
        await page.WaitForURLAsync("**/login", new() { Timeout = UiTimeout });

        await page.GotoAsync($"{_fixture.ApiBaseUrl}/accounts");

        await page.WaitForURLAsync("**/login", new() { Timeout = UiTimeout });
    }

    [Fact]
    public async Task Logout_OnMobileViewport_ClosesNavMenu()
    {
        var username = await RegisterUserAsync(_fixture);

        await using var context = await _fixture.Browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = 375, Height = 812 }
        });
        var page = await context.NewPageAsync();
        await SignInAsync(page, _fixture, username);

        var navMenu = page.Locator(".nav-scrollable");
        await page.Locator(".navbar-toggler").ClickAsync();
        await Assertions.Expect(navMenu).Not.ToHaveClassAsync(new Regex(@"\bcollapse\b"));

        await LogoutButton(page).ClickAsync();

        await Assertions.Expect(navMenu).ToHaveClassAsync(new Regex(@"\bcollapse\b"), new() { Timeout = 10_000 });
    }
}
