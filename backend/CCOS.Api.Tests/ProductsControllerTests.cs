using System.Net;
using System.Net.Http.Json;
using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
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
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var customizedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });
            });
        });

        _scope = customizedFactory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await _db.Database.EnsureCreatedAsync();

        // Remove dev data added by EnsureCreatedAndSeededAsync (which happens during builder.Build())
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM OrderLines");
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM TicketRegistrations");
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM Orders");
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM Products");
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM Events");
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM Venues");

        // Seed test data
        _db.Products.AddRange(
            new Product { Category = "Apparel", Name = "Active Shirt", Price = 25m, MemberPrice = 20m, StockQuantity = 10, IsActive = true },
            new Product { Category = "Apparel", Name = "Inactive Hat", Price = 15m, MemberPrice = 10m, StockQuantity = 5, IsActive = false }
        );
        await _db.SaveChangesAsync();
    }

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
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

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
        Assert.Single(products); // Only Active Shirt, not Inactive Hat
        Assert.Equal("Active Shirt", products[0].Name);
        Assert.True(products[0].IsActive);

        // Currently CanSeeMemberPrice() returns true for everyone
        Assert.Equal(20m, products[0].MemberPrice);
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

        // We wipe the database in InitializeAsync so it should only have the 2 products we added.
        Assert.Equal(2, products.Count); // Active Shirt, Inactive Hat
        Assert.Contains(products, p => p.Name == "Active Shirt");
        Assert.Contains(products, p => p.Name == "Inactive Hat");
        Assert.Contains(products, p => !p.IsActive);

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
