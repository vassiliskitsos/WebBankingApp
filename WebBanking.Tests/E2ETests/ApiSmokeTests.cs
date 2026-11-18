using Microsoft.Playwright;
using WebBanking.Tests.Infrastructure;

namespace WebBanking.Tests.E2ETests;

[Collection(E2ECollection.Name)]
public sealed class ApiSmokeTests
{
    private readonly E2EFixture _fixture;

    public ApiSmokeTests(E2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Browser_CanOpenOpenApiDocument()
    {
        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync($"{_fixture.ApiBaseUrl}/openapi/v1.json");

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Αναμενόταν 2xx, ήρθε {response.Status}.");
    }

    [Fact]
    public async Task Browser_CanOpenBlazorHomePage()
    {
        await using var context = await _fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(_fixture.ApiBaseUrl);

        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Hello, world!", new() { Timeout = 30_000 });
    }
}
