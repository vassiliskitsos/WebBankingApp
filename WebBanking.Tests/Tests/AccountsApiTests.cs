using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebBanking.Api.Data;
using WebBanking.Api.Data.Entities;
using WebBanking.Api.Services;
using WebBanking.Contracts.Accounts;
using WebBanking.Contracts.Auth;
using WebBanking.Tests.Infrastructure;

namespace WebBanking.Tests.Tests;

public sealed class AccountsApiTests
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public AccountsApiTests(PostgresFixture postgres)
    {
        _factory = new ApiFactory(postgres.ConnectionString);
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetAccounts_ReturnsOnlyCallersAccounts()
    {
        var owner = await CreateUserAsync(Role.Customer);
        var other = await CreateUserAsync(Role.Customer);

        var ownAccount = await CreateAccountAsync(owner.Id, 150.25m);
        var foreignAccount = await CreateAccountAsync(other.Id, 9999.99m);

        var token = await CreateTokenAsync(owner);

        var response = await SendGetAccountsAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var accounts = await response.Content.ReadFromJsonAsync<List<AccountDto>>();

        Assert.NotNull(accounts);

        var account = Assert.Single(accounts);

        Assert.Equal(ownAccount.Id, account.Id);
        Assert.Equal(ownAccount.Iban, account.Iban);
        Assert.Equal(150.25m, account.Balance);
        Assert.Equal("EUR", account.Currency);

        Assert.DoesNotContain(foreignAccount.Iban, body);
    }

    [Fact]
    public async Task GetAccounts_WithNoAccounts_ReturnsEmptyList()
    {
        var user = await CreateUserAsync(Role.Customer);

        var token = await CreateTokenAsync(user);

        var response = await SendGetAccountsAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var accounts = await response.Content.ReadFromJsonAsync<List<AccountDto>>();

        Assert.NotNull(accounts);
        Assert.Empty(accounts);
    }

    [Fact]
    public async Task GetAccounts_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAccounts_WithAdminToken_ReturnsForbidden()
    {
        var admin = await CreateUserAsync(Role.Admin);

        var token = await CreateTokenAsync(admin);

        var response = await SendGetAccountsAsync(token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAccounts_TwoCustomers_EachSeesOnlyOwnAccounts()
    {
        var alice = await CreateUserAsync(Role.Customer);
        var bob = await CreateUserAsync(Role.Customer);

        var aliceAccounts = new[]
        {
            await CreateAccountAsync(alice.Id, 10m),
            await CreateAccountAsync(alice.Id, 20m)
        };
        var bobAccounts = new[]
        {
            await CreateAccountAsync(bob.Id, 30m)
        };

        var aliceResult = await GetAccountsAsync(await CreateTokenAsync(alice));
        var bobResult = await GetAccountsAsync(await CreateTokenAsync(bob));

        Assert.Equal(
            aliceAccounts.Select(a => a.Id).Order(),
            aliceResult.Select(a => a.Id));
        Assert.Equal(
            bobAccounts.Select(a => a.Id),
            bobResult.Select(a => a.Id));

        Assert.Empty(aliceResult.Select(a => a.Iban).Intersect(bobResult.Select(a => a.Iban)));
    }

    [Fact]
    public async Task GetAccounts_UserWithoutAccounts_DoesNotSeeOtherUsersAccounts()
    {
        var owner = await CreateUserAsync(Role.Customer);
        var other = await CreateUserAsync(Role.Customer);

        await CreateAccountAsync(owner.Id, 500m);
        await CreateAccountAsync(owner.Id, 600m);

        var otherResult = await GetAccountsAsync(await CreateTokenAsync(other));

        Assert.Empty(otherResult);
    }

    [Fact]
    public async Task GetAccounts_AfterRealLogin_ReturnsOwnAccounts()
    {
        var owner = await CreateUserAsync(Role.Customer);
        var other = await CreateUserAsync(Role.Customer);

        var ownAccount = await CreateAccountAsync(owner.Id, 42.50m);
        await CreateAccountAsync(other.Id, 1m);

        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Username = owner.Username, Password = TestPassword });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(auth);

        var accounts = await GetAccountsAsync(auth.Token);

        var account = Assert.Single(accounts);
        Assert.Equal(ownAccount.Id, account.Id);
        Assert.Equal(ownAccount.Iban, account.Iban);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("tampered-signature")]
    public async Task GetAccounts_WithInvalidToken_ReturnsUnauthorized(string kind)
    {
        var owner = await CreateUserAsync(Role.Customer);
        await CreateAccountAsync(owner.Id, 100m);

        var token = kind == "garbage"
            ? "not-a-jwt"
            : TamperSignature(await CreateTokenAsync(owner));

        var response = await SendGetAccountsAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAccounts_WithTokenForUnknownUser_ReturnsEmptyList()
    {
        var owner = await CreateUserAsync(Role.Customer);
        await CreateAccountAsync(owner.Id, 100m);

        var ghost = new User
        {
            Id = int.MaxValue,
            Username = "ghost",
            Role = Role.Customer
        };

        var result = await GetAccountsAsync(await CreateTokenAsync(ghost));

        Assert.Empty(result);
    }

    private const string TestPassword = "Some-password-123";

    private static string TamperSignature(string token)
    {
        var parts = token.Split('.');
        var signature = parts[2].ToCharArray();
        signature[0] = signature[0] == 'A' ? 'B' : 'A';
        parts[2] = new string(signature);
        return string.Join('.', parts);
    }

    private async Task<List<AccountDto>> GetAccountsAsync(string token)
    {
        var response = await SendGetAccountsAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var accounts = await response.Content.ReadFromJsonAsync<List<AccountDto>>();

        Assert.NotNull(accounts);

        return accounts;
    }

    private Task<HttpResponseMessage> SendGetAccountsAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/accounts");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return _client.SendAsync(request);
    }

    private async Task<string> CreateTokenAsync(User user)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        return tokens.GenerateToken(user).Token;
    }

    private async Task<User> CreateUserAsync(Role role)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var user = new User
        {
            Username = $"accounts-{Guid.NewGuid():N}",
            Role = role
        };

        user.PasswordHash = passwordHasher.HashPassword(user, TestPassword);

        db.Users.Add(user);

        await db.SaveChangesAsync();

        return user;
    }

    private async Task<Account> CreateAccountAsync(int userId, decimal balance)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var account = new Account
        {
            UserId = userId,
            Iban = $"GR{Guid.NewGuid():N}",
            Balance = balance,
            Currency = "EUR"
        };

        db.Accounts.Add(account);

        await db.SaveChangesAsync();

        return account;
    }
}
