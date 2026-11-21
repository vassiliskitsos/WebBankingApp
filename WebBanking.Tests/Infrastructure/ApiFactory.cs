using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace WebBanking.Tests.Infrastructure;

public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString
            });
        });

        // Program.cs reads these while the builder is created, so they must be host settings.
        builder.UseSetting("JWT_SECRET", "integration-tests-only-secret-key-32b!");
        builder.UseSetting("JWT_ISSUER", "WebBankingApp.Tests");
        builder.UseSetting("JWT_EXPIRES_MINUTES", "60");
    }
}
