using Microsoft.EntityFrameworkCore;
using Npgsql;
using WebBanking.Api.Data;
using WebBanking.Api.Data.Entities;
using WebBanking.Tests.Infrastructure;

namespace WebBanking.Tests.Tests;

// Applies the real migrations to a clean PostgreSQL container and checks the constraints from KAN-25.
public sealed class DatabaseSchemaTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;

    public DatabaseSchemaTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migrations_AreAllApplied()
    {
        await using var db = CreateContext();

        var pending = await db.Database.GetPendingMigrationsAsync();

        Assert.Empty(pending);
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("_InitialCreate"));
    }

    [Fact]
    public async Task Username_MustBeUnique()
    {
        await using var db = CreateContext();
        db.Users.Add(NewUser("duplicate-user"));
        await db.SaveChangesAsync();

        await using var other = CreateContext();
        other.Users.Add(NewUser("duplicate-user"));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
        AssertUniqueViolation(ex, "IX_Users_Username");
    }

    [Fact]
    public async Task Iban_MustBeUnique()
    {
        await using var db = CreateContext();
        var user = NewUser("iban-owner");
        user.Accounts.Add(NewAccount("GR0000000000000000000000001"));
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await using var other = CreateContext();
        var otherUser = NewUser("iban-copier");
        otherUser.Accounts.Add(NewAccount("GR0000000000000000000000001"));
        other.Users.Add(otherUser);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
        AssertUniqueViolation(ex, "IX_Accounts_Iban");
    }

    [Fact]
    public async Task Balance_KeepsTwoDecimalPlaces()
    {
        int accountId;
        await using (var db = CreateContext())
        {
            var user = NewUser("decimal-owner");
            var account = NewAccount("GR0000000000000000000000002");
            account.Balance = 1234567890123456.78m;
            user.Accounts.Add(account);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            accountId = account.Id;
        }

        await using var reader = CreateContext();
        var stored = await reader.Accounts.SingleAsync(a => a.Id == accountId);

        Assert.Equal(1234567890123456.78m, stored.Balance);
    }

    [Fact]
    public async Task ConcurrentAccountUpdates_SecondWriterIsRejected()
    {
        int accountId;
        await using (var db = CreateContext())
        {
            var user = NewUser("concurrency-owner");
            var account = NewAccount("GR0000000000000000000000003");
            account.Balance = 100m;
            user.Accounts.Add(account);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            accountId = account.Id;
        }

        await using var first = CreateContext();
        await using var second = CreateContext();
        var firstCopy = await first.Accounts.SingleAsync(a => a.Id == accountId);
        var secondCopy = await second.Accounts.SingleAsync(a => a.Id == accountId);

        firstCopy.Balance -= 30m;
        await first.SaveChangesAsync();

        secondCopy.Balance -= 50m;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    // Makes sure the save failed because of this unique index, not some other database error.
    private static void AssertUniqueViolation(DbUpdateException ex, string indexName)
    {
        var postgres = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal(indexName, postgres.ConstraintName);
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options);

    private static User NewUser(string username) => new()
    {
        Username = username,
        PasswordHash = "not-a-real-hash",
        Role = Role.Customer
    };

    private static Account NewAccount(string iban) => new()
    {
        Iban = iban,
        Balance = 0m,
        Currency = "EUR"
    };
}
