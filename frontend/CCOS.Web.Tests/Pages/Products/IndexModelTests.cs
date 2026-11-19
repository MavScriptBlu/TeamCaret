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

    // ---- Sort (CAR-75) ----

    // A member's view: every product has a member price. The hat's member price ties the mug's
    // regular price, so price sorting has to use what the member pays.
    private void RespondWithSortSamples() =>
        _mockHttp.When(HttpMethod.Get, "http://localhost/api/products")
                 .Respond("application/json", JsonSerializer.Serialize(new List<ProductDto>
                 {
                     new(1, "Apparel", "T-Shirt", "A nice shirt", 20.00m, 15.00m, 10, true),
                     new(2, "Accessories", "mug", "A cool mug", 10.00m, null, 5, true),
                     new(3, "Apparel", "Hat", "A stylish hat", 15.00m, 9.00m, 5, true),
                 }));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-sort")]
    [InlineData(IndexModel.SortByName)]
    public async Task OnGetAsync_DefaultOrUnknownSort_SortsByNameIgnoringCase(string? sort)
    {
        RespondWithSortSamples();
        _model.Sort = sort;

        await _model.OnGetAsync(CancellationToken.None);

        string[] expected = ["Hat", "mug", "T-Shirt"];
        Assert.Equal(expected, _model.Products.Select(product => product.Name));
        Assert.Equal(IndexModel.SortByName, _model.SortOrDefault);
        Assert.False(_model.IsFiltered);
    }

    [Fact]
    public async Task OnGetAsync_SortByPriceLow_UsesThePriceTheVisitorPays()
    {
        RespondWithSortSamples();
        _model.Sort = IndexModel.SortByPriceLow;

        await _model.OnGetAsync(CancellationToken.None);

        // Hat $9 (member), mug $10 (no member price), T-Shirt $15 (member).
        string[] expected = ["Hat", "mug", "T-Shirt"];
        Assert.Equal(expected, _model.Products.Select(product => product.Name));
    }

    [Fact]
    public async Task OnGetAsync_SortByPriceHigh_PutsTheMostExpensiveFirst()
    {
        RespondWithSortSamples();
        _model.Sort = IndexModel.SortByPriceHigh;

        await _model.OnGetAsync(CancellationToken.None);

        string[] expected = ["T-Shirt", "mug", "Hat"];
        Assert.Equal(expected, _model.Products.Select(product => product.Name));
    }

    [Fact]
    public async Task OnGetAsync_SortWithFilter_SortsOnlyTheFilteredProducts()
    {
        RespondWithSortSamples();
        _model.Category = "Apparel";
        _model.Sort = IndexModel.SortByPriceHigh;

        await _model.OnGetAsync(CancellationToken.None);

        string[] expected = ["T-Shirt", "Hat"];
        Assert.Equal(expected, _model.Products.Select(product => product.Name));
        Assert.Equal(3, _model.TotalCount);
    }

    [Theory]
    [InlineData(20.00, 15.00, 15.00)] // member price lower: pays the member price
    [InlineData(20.00, null, 20.00)]  // not a member: pays the regular price
    [InlineData(20.00, 20.00, 20.00)] // same price either way
    public void PriceYouPay_PicksTheLowerPriceTheVisitorCanSee(double price, double? memberPrice, double expected)
    {
        // InlineData can't hold decimals, so the prices come in as doubles.
        var product = new ProductDto(1, "Apparel", "T-Shirt", null, (decimal)price, (decimal?)memberPrice, 1, true);

        Assert.Equal((decimal)expected, IndexModel.PriceYouPay(product));
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
