using System.Net;
using CCOS.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CCOS.Api.Tests;

/// <summary>
/// GET /health (CAR-84): "Healthy" when the database can be reached, 503 "Unhealthy" when it can't.
/// </summary>
public class HealthCheckTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    // Builds the API in test mode with the given database setup.
    private HttpClient CreateClient(Action<DbContextOptionsBuilder> useDatabase) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Testing", "true");
            builder.ConfigureTestServices(services => services.AddDbContext<AppDbContext>(useDatabase));
        }).CreateClient();

    [Fact]
    public async Task Health_database_reachable_is_healthy()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var client = CreateClient(options => options.UseSqlite(connection));

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_database_unreachable_is_503()
    {
        // Nothing listens on port 9, so the connection is refused right away.
        using var client = CreateClient(options =>
            options.UseNpgsql("Host=127.0.0.1;Port=9;Database=none;Username=none;Password=none;Timeout=2"));

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_needs_no_sign_in()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var client = CreateClient(options => options.UseSqlite(connection));

        var response = await client.GetAsync("/health");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
