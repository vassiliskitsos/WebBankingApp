
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace WebBanking.Tests.Tests;

public class JwtValidationTests
{
    private const string Secret =
        "test-only-secret-with-at-least-32-characters";

    private const string Issuer = "WebBankingApp.Tests";

    [Fact]
    public async Task WrongIssuer_Returns401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });

        var token = CreateToken("WrongIssuer", expired: false);

        client.DefaultRequestHeaders.Authorization =
            new("Bearer", token);

        var response = await client.GetAsync("/api/test-auth");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });

        var token = CreateToken(Issuer, expired: true);

        client.DefaultRequestHeaders.Authorization =
            new("Bearer", token);

        var response = await client.GetAsync("/api/test-auth");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidToken_Returns200()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });

        var token = CreateToken(Issuer, expired: false);

        client.DefaultRequestHeaders.Authorization =
            new("Bearer", token);

        var response = await client.GetAsync("/api/test-auth");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("JWT_SECRET", Secret);
                builder.UseSetting("JWT_ISSUER", Issuer);

                builder.ConfigureTestServices(services =>
                {
                    services.AddControllers()
                        .AddApplicationPart(
                            typeof(JwtProtectedTestController).Assembly);
                });
            });
    }

    private static string CreateToken(string issuer, bool expired)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(Secret));

        var credentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: null,
            notBefore: expired ? now.AddHours(-2) : now.AddMinutes(-1),
            expires: expired ? now.AddHours(-1) : now.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// Controller available only to the test host.
// It is not part of the production API.
[ApiController]
[Route("api/test-auth")]
[Authorize]
public sealed class JwtProtectedTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}
