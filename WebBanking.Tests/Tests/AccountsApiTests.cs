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

        user.PasswordHash = passwordHasher.HashPassword(user, "Some-password-123");

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
