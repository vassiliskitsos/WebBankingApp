
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace WebBanking.Tests.Tests;

public class JwtConfigurationTests
{
    private const string ValidSecret =
        "test-only-secret-with-at-least-32-characters";

    [Fact]
    public void Startup_WithShortSecret_ThrowsException()
    {
        using var factory = CreateFactory(
            "short",
            "WebBankingApp.Tests");

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateClient());

        Assert.Contains("JWT_SECRET", exception.ToString());
    }

    [Fact]
    public void Startup_WithEmptyIssuer_ThrowsException()
    {
        using var factory = CreateFactory(
            ValidSecret,
            "");

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateClient());

        Assert.Contains("JWT_ISSUER", exception.ToString());
    }

    private static WebApplicationFactory<Program> CreateFactory(
    string secret,
    string issuer)
{
    return new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseSetting("JWT_SECRET", secret);
            builder.UseSetting("JWT_ISSUER", issuer);
        });
}
}
