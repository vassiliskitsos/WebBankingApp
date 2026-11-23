using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WebBanking.Contracts.Accounts;
using WebBanking.Contracts.Auth;
using WebBanking.Tests.Infrastructure;

namespace WebBanking.Tests.E2ETests;

[Collection(E2ECollection.Name)]
public sealed class AccountsE2ETests : IDisposable
{
    private readonly HttpClient _http;

    public AccountsE2ETests(E2EFixture fixture)
    {
        _http = new HttpClient { BaseAddress = new Uri(fixture.ApiBaseUrl) };
    }

    public void Dispose() => _http.Dispose();

    [Fact]
    public async Task RegisterLoginAndListAccounts_AgainstRealBackend()
    {
        var username = $"e2e{Guid.NewGuid():N}"[..20];
        const string password = "123456";

        var register = await _http.PostAsJsonAsync("api/auth/register",
            new RegisterRequest { Username = username, Password = password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await _http.PostAsJsonAsync("api/auth/login",
            new LoginRequest { Username = username, Password = password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        using var request = new HttpRequestMessage(HttpMethod.Get, "api/accounts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var accounts = await _http.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, accounts.StatusCode);

        var body = await accounts.Content.ReadFromJsonAsync<List<AccountDto>>();
        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task GetAccounts_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _http.GetAsync("api/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
