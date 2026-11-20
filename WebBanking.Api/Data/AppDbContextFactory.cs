using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WebBanking.Api.Data;

// Used only by the `dotnet ef` tool, so it doesn't have to start Program.cs (which needs the JWT settings).
// There is deliberately no fallback: a missing setting must stop `database update`
// instead of quietly connecting to some default database.
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set the ConnectionStrings__Default environment variable before running dotnet ef (see README section 8).");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
