using System.Text.Json;
using Microsoft.Playwright;
using WebBanking.Contracts.Auth;
using WebBanking.Tests.Infrastructure;
using static WebBanking.Tests.Infrastructure.E2EAuthHelpers;

namespace WebBanking.Tests.E2ETests;

// S1-18: login, logout, protected page redirect and token clearing through the real UI,
// against the hosted ASP.NET backend and the Testcontainers PostgreSQL from E2EFixture
[Collection(E2ECollection.Name)]
public sealed class AuthFlowE2ETests
{
    private readonly E2EFixture _fixture;

    public AuthFlowE2ETests(E2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_WithValidCredentials_StoresTokenAndShowsSignedInUser()
    {
        var username = await RegisterUserAsync(_fixture);

        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await SignInAsync(page, _fixture, username);

        await Assertions.Expect(page).ToHaveURLAsync($"{_fixture.ApiBaseUrl}/", new() { Timeout = UiTimeout });
        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync($"Καλώς ήρθατε, {username}", new() { Timeout = UiTimeout });
        await Assertions.Expect(LogoutButton(page)).ToBeVisibleAsync();

        var stored = await ReadStoredSessionAsync(page);
        Assert.NotNull(stored);
        var session = JsonSerializer.Deserialize<AuthResponse>(stored);
        Assert.NotNull(session);
        Assert.Equal(username, session.Username);
        Assert.False(string.IsNullOrWhiteSpace(session.Token));
        Assert.True(session.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShowsErrorAndStoresNoToken()
    {
        var username = await RegisterUserAsync(_fixture);

        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await SubmitLoginFormAsync(page, _fixture, username, "wrong-password");

        await Assertions.Expect(page.Locator(".alert-danger")).ToBeVisibleAsync(new() { Timeout = UiTimeout });
        await Assertions.Expect(page.Locator("#password")).ToHaveValueAsync(string.Empty);
        await Assertions.Expect(page).ToHaveURLAsync($"{_fixture.ApiBaseUrl}/login");
        await Assertions.Expect(NavLoginLink(page)).ToBeVisibleAsync();
        Assert.Null(await ReadStoredSessionAsync(page));
    }

    [Fact]
    public async Task ProtectedPage_WhenNeverSignedIn_RedirectsToLogin_WithoutCallingApi()
    {
        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var accountsRequests = RecordAccountsApiRequests(page);

        await page.GotoAsync($"{_fixture.ApiBaseUrl}/accounts");

        await page.WaitForURLAsync("**/login", new() { Timeout = UiTimeout });
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Σύνδεση" }))
            .ToBeVisibleAsync(new() { Timeout = UiTimeout });
        Assert.Empty(accountsRequests);
        Assert.Null(await ReadStoredSessionAsync(page));
    }

    [Fact]
    public async Task Logout_ThenReload_StaysSignedOut()
    {
        var username = await RegisterUserAsync(_fixture);

        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await SignInAsync(page, _fixture, username);
        await SignOutAsync(page);

        // a reload restarts the WASM app, which restores the session from sessionStorage if one is left
        await page.ReloadAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Σύνδεση" }))
            .ToBeVisibleAsync(new() { Timeout = UiTimeout });
        await Assertions.Expect(NavLoginLink(page)).ToBeVisibleAsync(new() { Timeout = UiTimeout });
        await Assertions.Expect(LogoutButton(page, includeHidden: true)).ToHaveCountAsync(0);
        Assert.Null(await ReadStoredSessionAsync(page));

        await page.GotoAsync($"{_fixture.ApiBaseUrl}/accounts");
        await page.WaitForURLAsync("**/login", new() { Timeout = UiTimeout });
    }

    [Fact]
    public async Task Logout_ThenBrowserBack_DoesNotShowAccountsOrSendToken()
    {
        var username = await RegisterUserAsync(_fixture);

        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await SignInAsync(page, _fixture, username);

        // reach the protected page through the app, so it is in the browser history
        await page.GetByRole(AriaRole.Navigation).GetByRole(AriaRole.Link, new() { Name = "Λογαριασμοί" }).ClickAsync();
        await page.WaitForURLAsync("**/accounts", new() { Timeout = UiTimeout });
        var emptyAccounts = page.GetByText("Δεν βρέθηκαν λογαριασμοί");
        await Assertions.Expect(emptyAccounts).ToBeVisibleAsync(new() { Timeout = UiTimeout });

        await SignOutAsync(page);
        Assert.Null(await ReadStoredSessionAsync(page));

        var accountsRequests = RecordAccountsApiRequests(page);
        var visitedUrls = new List<string>();
        page.FrameNavigated += (_, frame) =>
        {
            if (frame == page.MainFrame)
            {
                visitedUrls.Add(frame.Url);
            }
        };

        await page.GoBackAsync();

        // the back step lands on /accounts, which sends a signed-out user straight back to /login
        await Assertions.Expect(page).ToHaveURLAsync($"{_fixture.ApiBaseUrl}/login", new() { Timeout = UiTimeout });
        Assert.Contains(visitedUrls, url => url.EndsWith("/accounts", StringComparison.Ordinal));
        await Assertions.Expect(emptyAccounts).ToBeHiddenAsync();
        await Assertions.Expect(NavLoginLink(page)).ToBeVisibleAsync();

        foreach (var request in accountsRequests)
        {
            Assert.Null(await request.HeaderValueAsync("authorization"));
        }
    }

    private static List<IRequest> RecordAccountsApiRequests(IPage page)
    {
        var requests = new List<IRequest>();
        page.Request += (_, request) =>
        {
            if (new Uri(request.Url).AbsolutePath.StartsWith("/api/accounts", StringComparison.OrdinalIgnoreCase))
            {
                requests.Add(request);
            }
        };

        return requests;
    }
}
