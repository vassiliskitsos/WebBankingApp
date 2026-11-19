using Npgsql;
using WebBanking.Tests.Infrastructure;

namespace WebBanking.Tests.Tests;

public sealed class InfrastructureSmokeTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public InfrastructureSmokeTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task PostgresContainer_AcceptsConnections()
    {
        await using var connection = new NpgsqlConnection(_postgres.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("select 1", connection);
        var result = await command.ExecuteScalarAsync();

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Api_StartsAndServesOpenApiDocument()
    {
        await using var factory = new ApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.True(response.IsSuccessStatusCode, $"Αναμενόταν 2xx, ήρθε {(int)response.StatusCode}.");
    }
}
