using Testcontainers.PostgreSql;

namespace WebBanking.Tests.Infrastructure;

// postgres container which starts before the tests run and gets deleted after
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("webbanking_tests")
        .WithUsername("webbanking")
        .WithPassword("test-only-password")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
