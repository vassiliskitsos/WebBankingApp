using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using WebBanking.Api.Data.Entities;
using WebBanking.Api.Services;

namespace WebBanking.Tests.Tests;

public sealed class JwtTokenServiceTests
{
    private const string Secret = "test-only-signing-secret-at-least-32-bytes";
    private const int LifetimeMinutes = 15;

    [Theory]
    [InlineData(Role.Customer)]
    [InlineData(Role.Admin)]
    public void GenerateToken_UsesUsersRole(Role role)
    {
        var service = new JwtTokenService(CreateConfiguration());
        var user = new User { Id = 7, Username = "test-user", Role = role };

        var response = service.GenerateToken(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);

        Assert.Equal(role.ToString(), response.Role);
        Assert.Equal(role.ToString(), Assert.Single(token.Claims, claim => claim.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void GenerateToken_UsesConfiguredLifetime()
    {
        var service = new JwtTokenService(CreateConfiguration());
        var earliestExpiry = DateTime.UtcNow.AddMinutes(LifetimeMinutes);

        var response = service.GenerateToken(new User { Id = 7, Username = "test-user" });

        var latestExpiry = DateTime.UtcNow.AddMinutes(LifetimeMinutes);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);
        Assert.InRange(response.ExpiresAtUtc, earliestExpiry, latestExpiry);
        Assert.Equal(LifetimeMinutes * 60, (token.ValidTo - token.ValidFrom).TotalSeconds);
        Assert.Equal(new DateTimeOffset(response.ExpiresAtUtc).ToUnixTimeSeconds(),
            new DateTimeOffset(token.ValidTo).ToUnixTimeSeconds());
    }

    [Theory]
    [InlineData("JWT_SECRET", null)]
    [InlineData("JWT_SECRET", "short-test-secret")]
    [InlineData("JWT_ISSUER", null)]
    [InlineData("JWT_ISSUER", " ")]
    [InlineData("JWT_EXPIRES_MINUTES", null)]
    [InlineData("JWT_EXPIRES_MINUTES", "invalid")]
    [InlineData("JWT_EXPIRES_MINUTES", "0")]
    [InlineData("JWT_EXPIRES_MINUTES", "-1")]
    public void Constructor_InvalidConfiguration_SanitizesException(string key, string? value)
    {
        var configuration = CreateConfiguration();
        configuration[key] = value;

        var exception = Assert.Throws<InvalidOperationException>(() => new JwtTokenService(configuration));

        Assert.Contains(key, exception.Message);
        Assert.DoesNotContain(Secret, exception.ToString());
        if (key == "JWT_SECRET" && value is not null)
        {
            Assert.DoesNotContain(value, exception.ToString());
        }
    }

    private static IConfiguration CreateConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = Secret,
            ["JWT_ISSUER"] = "WebBankingApp.Tests",
            ["JWT_EXPIRES_MINUTES"] = LifetimeMinutes.ToString()
        })
        .Build();
}
