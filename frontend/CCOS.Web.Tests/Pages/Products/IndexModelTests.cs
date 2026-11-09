using System.Net;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Pages.Products;
using CCOS.Web.Services;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;

namespace CCOS.Web.Tests.Pages.Products;

public class IndexModelTests
{
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly HttpClient _httpClient;
    private readonly ProductsApiClient _apiClient;
    private readonly Mock<ILogger<IndexModel>> _mockLogger;
    private readonly IndexModel _model;

    public IndexModelTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = _mockHttp.ToHttpClient();
        _httpClient.BaseAddress = new Uri("http://localhost");
        _apiClient = new ProductsApiClient(_httpClient);
        _mockLogger = new Mock<ILogger<IndexModel>>();
        _model = new IndexModel(_apiClient, _mockLogger.Object);
    }

    [Fact]
    public async Task OnGetAsync_WithProducts_PopulatesModel()
    {
        // Arrange
        var products = new List<ProductDto>
        {
            new ProductDto(1, "Apparel", "T-Shirt", "A nice shirt", 20.00m, 15.00m, 10, true),
            new ProductDto(2, "Accessories", "Mug", "A cool mug", 10.00m, 8.00m, 0, true),
            new ProductDto(3, "Apparel", "Hat", "A stylish hat", 15.00m, 12.00m, 5, true)
        };

        _mockHttp.When(HttpMethod.Get, "http://localhost/api/products")
                 .Respond("application/json", JsonSerializer.Serialize(products));

        // Act
        await _model.OnGetAsync(CancellationToken.None);

        // Assert
        Assert.False(_model.LoadFailed);
        Assert.Equal(3, _model.TotalCount);
        Assert.Equal(3, _model.Products.Count);
        Assert.Equal(2, _model.Categories.Count);
        Assert.Contains("Accessories", _model.Categories);
        Assert.Contains("Apparel", _model.Categories);
        Assert.False(_model.IsFiltered);
    }

    [Fact]
    public async Task OnGetAsync_WithCategoryFilter_FiltersProducts()
    {
        // Arrange
        var products = new List<ProductDto>
        {
            new ProductDto(1, "Apparel", "T-Shirt", "A nice shirt", 20.00m, 15.00m, 10, true),
            new ProductDto(2, "Accessories", "Mug", "A cool mug", 10.00m, 8.00m, 0, true)
        };

        _mockHttp.When(HttpMethod.Get, "http://localhost/api/products")
                 .Respond("application/json", JsonSerializer.Serialize(products));

        _model.Category = "Apparel";

        // Act
        await _model.OnGetAsync(CancellationToken.None);

        // Assert
        Assert.False(_model.LoadFailed);
        Assert.Equal(2, _model.TotalCount); // TotalCount is before filtering
        Assert.Single(_model.Products);
        Assert.Equal("T-Shirt", _model.Products[0].Name);
        Assert.True(_model.IsFiltered);
    }

    [Fact]
    public async Task OnGetAsync_WithInStockOnlyFilter_FiltersProducts()
    {
        // Arrange
        var products = new List<ProductDto>
        {
            new ProductDto(1, "Apparel", "T-Shirt", "A nice shirt", 20.00m, 15.00m, 10, true),
            new ProductDto(2, "Accessories", "Mug", "A cool mug", 10.00m, 8.00m, 0, true)
        };

        _mockHttp.When(HttpMethod.Get, "http://localhost/api/products")
                 .Respond("application/json", JsonSerializer.Serialize(products));

        _model.InStockOnly = true;

        // Act
        await _model.OnGetAsync(CancellationToken.None);

        // Assert
        Assert.False(_model.LoadFailed);
        Assert.Equal(2, _model.TotalCount); // TotalCount is before filtering
        Assert.Single(_model.Products);
        Assert.Equal("T-Shirt", _model.Products[0].Name);
        Assert.True(_model.IsFiltered);
    }

    [Fact]
    public async Task OnGetAsync_WithBothFilters_FiltersProducts()
    {
        // Arrange
        var products = new List<ProductDto>
        {
            new ProductDto(1, "Apparel", "T-Shirt", "A nice shirt", 20.00m, 15.00m, 10, true),
            new ProductDto(2, "Accessories", "Mug", "A cool mug", 10.00m, 8.00m, 0, true),
            new ProductDto(3, "Apparel", "Hat", "A stylish hat", 15.00m, 12.00m, 0, true)
        };

        _mockHttp.When(HttpMethod.Get, "http://localhost/api/products")
                 .Respond("application/json", JsonSerializer.Serialize(products));

        _model.Category = "Apparel";
        _model.InStockOnly = true;

        // Act
        await _model.OnGetAsync(CancellationToken.None);

        // Assert
        Assert.False(_model.LoadFailed);
        Assert.Equal(3, _model.TotalCount); // TotalCount is before filtering
        Assert.Single(_model.Products);
        Assert.Equal("T-Shirt", _model.Products[0].Name);
        Assert.True(_model.IsFiltered);
    }

    [Fact]
    public async Task OnGetAsync_WhenApiFails_SetsLoadFailedAndLogsError()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, "http://localhost/api/products")
                 .Respond(HttpStatusCode.InternalServerError);

        // Act
        await _model.OnGetAsync(CancellationToken.None);

        // Assert
        Assert.True(_model.LoadFailed);
        Assert.Empty(_model.Products);
        Assert.Empty(_model.Categories);
        Assert.Equal(0, _model.TotalCount);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<HttpRequestException>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_WhenApiReturnsInvalidJson_SetsLoadFailedAndLogsError()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, "http://localhost/api/products")
                 .Respond("application/json", "{ invalid json }");

        // Act
        await _model.OnGetAsync(CancellationToken.None);

        // Assert
        Assert.True(_model.LoadFailed);
        Assert.Empty(_model.Products);
        Assert.Empty(_model.Categories);
        Assert.Equal(0, _model.TotalCount);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<JsonException>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}
