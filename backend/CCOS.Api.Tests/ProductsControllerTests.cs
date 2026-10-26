using System.Net;
using System.Net.Http.Json;
using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CCOS.Api.Tests;

public class ProductsControllerTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private SqliteConnection _connection = null!;
    private IServiceScope _scope = null!;
    private AppDbContext _db = null!;

    public ProductsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // Tell Program.cs to skip its DbContext registration - tests handle it.
        Environment.SetEnvironmentVariable("CCOS_TEST_MODE", "1");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var customizedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });
            });
        });

        _scope = customizedFactory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await _db.Database.EnsureCreatedAsync();

        // Seed test data
        _db.Products.AddRange(
            new Product { Category = "Apparel", Name = "Active Shirt", Price = 25m, MemberPrice = 20m, StockQuantity = 10, IsActive = true },
            new Product { Category = "Apparel", Name = "Inactive Hat", Price = 15m, MemberPrice = 10m, StockQuantity = 5, IsActive = false }
        );
        await _db.SaveChangesAsync();
    }

    // Cleanup after tests
    public async Task DisposeAsync()
    {
        _scope.Dispose();
        await _connection.CloseAsync();
        _connection.Dispose();
    }

    private HttpClient CreateClient()
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });
            });
        }).CreateClient();
    }

    [Fact]
    public async Task List_Default_ReturnsOnlyActiveMerchandise()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/api/products");

        // Assert
        response.EnsureSuccessStatusCode();
        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();

        Assert.NotNull(products);
        // should include Active Shirt from test data, exclude Inactive Hat
        Assert.Contains(products, p => p.Name == "Active Shirt" && p.IsActive);
        Assert.DoesNotContain(products, p => p.Name == "Inactive Hat");
        Assert.All(products, p => Assert.True(p.IsActive));

        // Currently CanSeeMemberPrice() returns true for everyone
        var activeShirt = products.Single(p => p.Name == "Active Shirt");
        Assert.Equal(20m, activeShirt.MemberPrice);
    }

    [Fact]
    public async Task List_IncludeInactive_ReturnsAllMerchandise()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/api/products?includeInactive=true");

        // Assert
        response.EnsureSuccessStatusCode();
        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();

        Assert.NotNull(products);

        // Should include both active and inactive test products (plus any seed data)
        Assert.Contains(products, p => p.Name == "Active Shirt");
        Assert.Contains(products, p => p.Name == "Inactive Hat");
        // Inactive Hat proves includeInactive=true works
        Assert.Contains(products, p => p.Name == "Inactive Hat" && !p.IsActive);

        // Member prices are included
        Assert.All(products, p => Assert.NotNull(p.MemberPrice));
    }

    [Fact]
    public async Task Get_ExistingActiveProduct_ReturnsProduct()
    {
        // Arrange
        var client = CreateClient();
        var activeProduct = await _db.Products.FirstAsync(p => p.Name == "Active Shirt");

        // Act
        var response = await client.GetAsync($"/api/products/{activeProduct.ProductId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var product = await response.Content.ReadFromJsonAsync<ProductDto>();

        Assert.NotNull(product);
        Assert.Equal("Active Shirt", product.Name);
        Assert.True(product.IsActive);
    }

    [Fact]
    public async Task Get_InactiveProduct_ReturnsNotFound()
    {
        // Arrange
        var client = CreateClient();
        var inactiveProduct = await _db.Products.IgnoreQueryFilters().FirstAsync(p => p.Name == "Inactive Hat");

        // Act
        var response = await client.GetAsync($"/api/products/{inactiveProduct.ProductId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_NonexistentProduct_ReturnsNotFound()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/api/products/999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
