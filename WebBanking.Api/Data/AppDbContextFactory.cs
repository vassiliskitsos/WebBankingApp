using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WebBanking.Api.Data;

// Used only by the `dotnet ef` tool, so it doesn't have to start Program.cs (which needs the JWT settings).
// `migrations add` never connects, so the placeholder is enough there.
// For `database update`, set ConnectionStrings__Default in the environment.
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Host=localhost;Database=webbanking";
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
