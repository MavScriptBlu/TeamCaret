using Testcontainers.PostgreSql;

namespace CCOS.Api.Tests;

/// <summary>
/// Provides a real PostgreSQL database for tests of PostgreSQL-only behavior such as xmin.
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