using Testcontainers.PostgreSql;

namespace CCOS.Api.Tests;

/// <summary>
/// Starts an isolated PostgreSQL database for tests that depend on PostgreSQL behavior,
/// such as the xmin concurrency token.
/// </summary>
public sealed class PostgresApiTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("ccos_test")
        .WithUsername("ccos")
        .WithPassword("ccos_test_password")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
