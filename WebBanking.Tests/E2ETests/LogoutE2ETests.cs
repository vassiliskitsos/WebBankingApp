using System.Net.Http.Json;
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

    [Fact]
    public async Task Logout_ClearsUiState_AndProtectedPageRedirectsToLogin()
    {
        var username = $"e2e{Guid.NewGuid():N}"[..20];
        const string password = "123456";

        using (var http = new HttpClient { BaseAddress = new Uri(_fixture.ApiBaseUrl) })
        {
            var register = await http.PostAsJsonAsync("api/auth/register",
                new RegisterRequest { Username = username, Password = password });
            Assert.Equal(System.Net.HttpStatusCode.Created, register.StatusCode);
        }

        await using var context = await _fixture.Browser.NewContextAsync();
        await _fixture.RouteDevApiToTestServerAsync(context);
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{_fixture.ApiBaseUrl}/login");
        await page.FillAsync("#username", username);
        await page.FillAsync("#password", password);
        await page.ClickAsync("button[type=submit]");

        var signOut = page.GetByRole(AriaRole.Button, new() { Name = "Αποσύνδεση" });
        await Assertions.Expect(signOut).ToBeVisibleAsync(new() { Timeout = 30_000 });

        await signOut.ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Navigation).GetByRole(AriaRole.Link, new() { Name = "Σύνδεση", Exact = true }))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });
        Assert.Empty(await context.CookiesAsync());

        await page.GotoAsync($"{_fixture.ApiBaseUrl}/accounts");

        await page.WaitForURLAsync("**/login", new() { Timeout = 30_000 });
    }
}
