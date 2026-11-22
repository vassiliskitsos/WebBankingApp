using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WebBanking.Api.Data;
using WebBanking.Tests.Infrastructure;
using WebBanking.Contracts.Auth;

namespace WebBanking.Tests.Tests;

public sealed class AuthControllerTests
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;

    public AuthControllerTests(PostgresFixture postgres)
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
    public async Task Register_ValidRequest_ReturnsCreatedAndStoresHashedPassword()
    {
        var username = $"register-{Guid.NewGuid():N}";
        const string password = "StrongPassword123!";

        using var factory = new ApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest
            {
                Username = username,
                Password = password
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var responseContent = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(password, responseContent);
        Assert.DoesNotContain(
            "passwordHash",
            responseContent,
            StringComparison.OrdinalIgnoreCase);

        var registration = await response.Content
            .ReadFromJsonAsync<RegistrationResponse>();

        Assert.NotNull(registration);
        Assert.True(registration.UserId > 0);

        await using var db = CreateContext();

        var user = await db.Users.SingleAsync(
            u => u.Username == username);

        Assert.NotEqual(password, user.PasswordHash);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsConflict()
    {
        var username = $"duplicate-{Guid.NewGuid():N}";

        using var factory = new ApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateClient();

        var request = new RegisterRequest
        {
            Username = username,
            Password = "StrongPassword123!"
        };

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/auth/register", request);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        using var secondResponse = await client.PostAsJsonAsync(
            "/api/auth/register", request);

        Assert.Equal(
            HttpStatusCode.Conflict,
            secondResponse.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRequest_ReturnsBadRequest()
    {
        using var factory = new ApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateClient();

        var request = new RegisterRequest
        {
            Username = "ab",
            Password = "123456"
        };

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register", request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task Register_MalformedJson_ReturnsBadRequest()
    {
        using var factory = new ApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateClient();

        using var content = new StringContent(
            "{ invalid json",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync(
            "/api/auth/register",
            content);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options);

    private sealed record RegistrationResponse(int UserId);
}
