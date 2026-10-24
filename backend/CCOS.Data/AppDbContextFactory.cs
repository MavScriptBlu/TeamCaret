using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CCOS.Data;

/// <summary>
/// Builds an AppDbContext for the `dotnet ef` tools (migrations add, database update).
/// The running API never uses this class; it sets up its own database in Program.cs.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    // Same UserSecretsId as backend/CCOS.Api/CCOS.Api.csproj, so `dotnet ef` reads the
    // exact connection string the API uses. Change both together if it ever changes.
    private const string ApiUserSecretsId = "e6c1aaec-f6fd-466a-8ee2-3acbd3fe08f9";

    /// <summary>
    /// Finds the connection string, in this order:
    /// 1. The CCOS_CONNECTION_STRING environment variable (CI uses this).
    /// 2. ConnectionStrings:CCOS in the API's user secrets (what you set on your own machine).
    /// Throws with setup steps if neither one is there.
    /// </summary>
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(ApiUserSecretsId)
            .Build();

        var connectionString = Environment.GetEnvironmentVariable("CCOS_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = configuration.GetConnectionString("CCOS");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No database connection string found. Start PostgreSQL with `task db`, then set the " +
                "user secret ConnectionStrings:CCOS for backend/CCOS.Api (steps in backend/README.md).");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
