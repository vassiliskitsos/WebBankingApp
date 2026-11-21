using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using WebBanking.Api.Data;
using WebBanking.Api.Data.Entities;
using WebBanking.Contracts.Auth;
using WebBanking.Tests.Infrastructure;

namespace WebBanking.Tests.Tests;

public sealed class LoginIntegrationTests
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string TestSecret =
        "integration-tests-only-secret-key-32b!";

    private const string TestIssuer =
        "WebBankingApp.Tests";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public LoginIntegrationTests(PostgresFixture postgres)
    {
        _factory = new ApiFactory(postgres.ConnectionString);
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await using var scope =
            _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSignedJwt()
    {
        const string password = "Correct-password-123";

        var user = await CreateUserAsync(password);

        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Username = user.Username,
                Password = password
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var authResponse =
            await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(authResponse);
        Assert.Equal(user.Id, authResponse.UserId);
        Assert.Equal(user.Username, authResponse.Username);
        Assert.Equal("Customer", authResponse.Role);
        Assert.False(string.IsNullOrWhiteSpace(authResponse.Token));

        var handler = new JwtSecurityTokenHandler();

        var principal = handler.ValidateToken(
            authResponse.Token,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = TestIssuer,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(TestSecret)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            },
            out var validatedToken);

        Assert.Equal(
            user.Id.ToString(),
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        Assert.Equal(
            user.Username,
            principal.Identity?.Name);

        Assert.Equal(
            "Customer",
            principal.FindFirst(ClaimTypes.Role)?.Value);

        var jwt = Assert.IsType<JwtSecurityToken>(validatedToken);

        Assert.True(
            Math.Abs(
                (authResponse.ExpiresAtUtc - jwt.ValidTo)
                .TotalSeconds) < 1);

        Assert.InRange(
            (authResponse.ExpiresAtUtc - DateTime.UtcNow)
                .TotalMinutes,
            59,
            60.1);
    }

    [Fact]
    public async Task Login_WithIncorrectPassword_ReturnsUnauthorized()
    {
        var user = await CreateUserAsync("Correct-password-123");

        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Username = user.Username,
                Password = "Wrong-password-123"
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownUsername_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Username = "unknown-user",
                Password = "Some-password-123"
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Login_WithoutUsername_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Password = "Some-password-123"
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task Login_WithoutPassword_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Username = "some-user"
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    private async Task<User> CreateUserAsync(string password)
    {
        await using var scope =
            _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var passwordHasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<User>>();

        var user = new User
        {
            Username = $"login-{Guid.NewGuid():N}",
            Role = Role.Customer
        };

        user.PasswordHash = passwordHasher.HashPassword(
            user,
            password);

        db.Users.Add(user);

        await db.SaveChangesAsync();

        return user;
    }
}
