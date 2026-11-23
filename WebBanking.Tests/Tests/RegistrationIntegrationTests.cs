using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebBanking.Api.Data;
using WebBanking.Api.Data.Entities;
using WebBanking.Contracts.Auth;
using WebBanking.Tests.Infrastructure;

namespace WebBanking.Tests.Tests;

public sealed class RegistrationIntegrationTests
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public RegistrationIntegrationTests(PostgresFixture postgres)
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
    public async Task Register_WithValidRequest_Returns201CreatedAndStoresNonPlaintextHashInPostgres()
    {
        var username = $"reg-user-{Guid.NewGuid():N}";
        const string plainPassword = "StrongPassword123!";

        using var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest
            {
                Username = username,
                Password = plainPassword
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(plainPassword, rawBody, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", rawBody, StringComparison.OrdinalIgnoreCase);

        using var jsonDoc = JsonDocument.Parse(rawBody);
        Assert.True(jsonDoc.RootElement.TryGetProperty("userId", out var userIdElement));
        var createdUserId = userIdElement.GetInt32();
        Assert.True(createdUserId > 0);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var savedUser = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == createdUserId);

        Assert.NotNull(savedUser);
        Assert.Equal(username, savedUser.Username);
        Assert.Equal(Role.Customer, savedUser.Role);
        Assert.False(string.IsNullOrWhiteSpace(savedUser.PasswordHash));
        Assert.NotEqual(plainPassword, savedUser.PasswordHash);
        Assert.DoesNotContain(plainPassword, savedUser.PasswordHash, StringComparison.Ordinal);

        // Verify directly against the PostgreSQL column value that no plaintext password was stored
        var storedHashes = await db.Database
            .SqlQuery<string>($"SELECT \"PasswordHash\" AS \"Value\" FROM \"Users\" WHERE \"Id\" = {createdUserId}")
            .ToListAsync();

        var rawStoredHash = Assert.Single(storedHashes);
        Assert.NotEqual(plainPassword, rawStoredHash);
        Assert.DoesNotContain(plainPassword, rawStoredHash, StringComparison.Ordinal);

        var verificationResult = passwordHasher.VerifyHashedPassword(
            savedUser,
            rawStoredHash,
            plainPassword);
        Assert.NotEqual(PasswordVerificationResult.Failed, verificationResult);
    }

    [Fact]
    public async Task Register_ThenLogin_SucceedsAgainstPersistedHashedPassword()
    {
        var username = $"reg-login-{Guid.NewGuid():N}";
        const string plainPassword = "ValidLoginPass123!";

        using var registerResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest
            {
                Username = username,
                Password = plainPassword
            });

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        using var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Username = username,
                Password = plainPassword
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Equal(username, auth.Username);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
    }

    [Theory]
    [InlineData("", "ValidPassword123")]
    [InlineData("ab", "ValidPassword123")]
    [InlineData("validuser", "")]
    [InlineData("validuser", "12345")]
    public async Task Register_WithInvalidRequest_Returns400BadRequestAndDoesNotPersistUser(
        string username,
        string password)
    {
        var candidateUsername = string.IsNullOrWhiteSpace(username) || username.Length < 3
            ? username
            : $"{username}-{Guid.NewGuid():N}";

        using var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest
            {
                Username = candidateUsername,
                Password = password
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        if (!string.IsNullOrWhiteSpace(candidateUsername))
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var exists = await db.Users.AnyAsync(u => u.Username == candidateUsername);
            Assert.False(exists);
        }
    }

    [Fact]
    public async Task Register_WithUsernameExceedingMaxLength_Returns400BadRequestAndDoesNotPersistUser()
    {
        var tooLongUsername = new string('u', 51);

        using var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest
            {
                Username = tooLongUsername,
                Password = "ValidPassword123"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.Username == tooLongUsername));
    }

    [Fact]
    public async Task Register_WithMalformedJson_Returns400BadRequest()
    {
        using var content = new StringContent(
            "{ invalid json",
            Encoding.UTF8,
            "application/json");

        using var response = await _client.PostAsync("/api/auth/register", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_Returns409ConflictAndKeepsSingleUserRecord()
    {
        var username = $"dup-user-{Guid.NewGuid():N}";

        using var firstResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest
            {
                Username = username,
                Password = "FirstPassword123!"
            });

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var duplicateResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest
            {
                Username = username,
                Password = "SecondPassword123!"
            });

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        using var errorDoc = JsonDocument.Parse(await duplicateResponse.Content.ReadAsStringAsync());
        Assert.True(errorDoc.RootElement.TryGetProperty("message", out var messageElement));
        Assert.False(string.IsNullOrWhiteSpace(messageElement.GetString()));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var matchingUsersCount = await db.Users.CountAsync(u => u.Username == username);
        Assert.Equal(1, matchingUsersCount);
    }
}
