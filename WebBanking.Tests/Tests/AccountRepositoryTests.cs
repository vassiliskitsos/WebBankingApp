using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebBanking.Api.Data;
using WebBanking.Api.Data.Entities;
using WebBanking.Api.Services.Interfaces;
using WebBanking.Tests.Infrastructure;

namespace WebBanking.Tests.Tests;

public sealed class AccountRepositoryTests
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly ApiFactory _factory;

    public AccountRepositoryTests(PostgresFixture postgres)
    {
        _factory = new ApiFactory(postgres.ConnectionString);
    }

    public async Task InitializeAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsOnlyThatUsersAccounts_OrderedById()
    {
        var owner = await CreateUserAsync();
        var other = await CreateUserAsync();

        var first = await CreateAccountAsync(owner.Id);
        var foreign = await CreateAccountAsync(other.Id);
        var second = await CreateAccountAsync(owner.Id);

        var result = await GetByUserIdAsync(owner.Id);

        Assert.Equal(new[] { first.Id, second.Id }, result.Select(a => a.Id));
        Assert.All(result, a => Assert.Equal(owner.Id, a.UserId));
        Assert.DoesNotContain(result, a => a.Id == foreign.Id);
    }

    [Fact]
    public async Task GetByUserIdAsync_UnknownUser_ReturnsEmpty()
    {
        var owner = await CreateUserAsync();
        await CreateAccountAsync(owner.Id);

        var result = await GetByUserIdAsync(int.MaxValue);

        Assert.Empty(result);
    }

    private async Task<IReadOnlyList<Account>> GetByUserIdAsync(int userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var repository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        return await repository.GetByUserIdAsync(userId);
    }

    private async Task<User> CreateUserAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new User
        {
            Username = $"repo-{Guid.NewGuid():N}",
            PasswordHash = "not-used",
            Role = Role.Customer
        };

        db.Users.Add(user);

        await db.SaveChangesAsync();

        return user;
    }

    private async Task<Account> CreateAccountAsync(int userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var account = new Account
        {
            UserId = userId,
            Iban = $"GR{Guid.NewGuid():N}",
            Balance = 0m,
            Currency = "EUR"
        };

        db.Accounts.Add(account);

        await db.SaveChangesAsync();

        return account;
    }
}
