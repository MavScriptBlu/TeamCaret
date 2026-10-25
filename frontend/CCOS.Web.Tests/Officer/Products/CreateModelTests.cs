using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CCOS.Shared;
using CCOS.Web.Pages.Officer.Products;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace CCOS.Web.Tests.Officer.Products;

public class CreateModelTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly Mock<ILogger<CreateModel>> _loggerMock;
    private readonly HttpClient _httpClient;
    private readonly ProductsApiClient _productsApi;
    private readonly CreateModel _model;

    public CreateModelTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<CreateModel>>();

        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };
        _productsApi = new ProductsApiClient(_httpClient);

        _model = new CreateModel(_productsApi, _loggerMock.Object)
        {
            PageContext = new PageContext(),
            TempData = new TempDataDictionary(new Microsoft.AspNetCore.Http.DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };
    }

    [Fact]
    public void OnGet_DoesNothing()
    {
        // Act
        _model.OnGet();

        // Assert
        // Nothing to assert, just making sure it executes without errors
    }

    [Fact]
    public async Task OnPostAsync_InvalidModelState_ReturnsPageResult()
    {
        // Arrange
        _model.ModelState.AddModelError("Name", "Name is required");
        _model.Input = new ProductInput();

        // Act
        var result = await _model.OnPostAsync(CancellationToken.None);

        // Assert
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_ApiCallFails_AddsModelErrorAndReturnsPageResult()
    {
        // Arrange
        _model.Input = new ProductInput
        {
            Category = "Category1",
            Name = "Product 1",
            Description = "Description 1",
            Price = 10.0m,
            MemberPrice = 8.0m,
            StockQuantity = 50
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest));

        // Act
        var result = await _model.OnPostAsync(CancellationToken.None);

        // Assert
        Assert.IsType<PageResult>(result);
        Assert.False(_model.ModelState.IsValid);
        Assert.True(_model.ModelState.ContainsKey(string.Empty));
        Assert.Equal("The product could not be created. Please try again", _model.ModelState[string.Empty]?.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_HttpRequestException_AddsModelErrorAndReturnsPageResult()
    {
        // Arrange
        _model.Input = new ProductInput
        {
            Category = "Category1",
            Name = "Product 1",
            Description = "Description 1",
            Price = 10.0m,
            MemberPrice = 8.0m,
            StockQuantity = 50
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException());

        // Act
        var result = await _model.OnPostAsync(CancellationToken.None);

        // Assert
        Assert.IsType<PageResult>(result);
        Assert.False(_model.ModelState.IsValid);
        Assert.True(_model.ModelState.ContainsKey(string.Empty));
        Assert.Equal("The product API could not be reached. Please try again later", _model.ModelState[string.Empty]?.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_TaskCanceledException_AddsModelErrorAndReturnsPageResult()
    {
        // Arrange
        _model.Input = new ProductInput
        {
            Category = "Category1",
            Name = "Product 1",
            Description = "Description 1",
            Price = 10.0m,
            MemberPrice = 8.0m,
            StockQuantity = 50
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new TaskCanceledException());

        // Act
        var result = await _model.OnPostAsync(CancellationToken.None);

        // Assert
        Assert.IsType<PageResult>(result);
        Assert.False(_model.ModelState.IsValid);
        Assert.True(_model.ModelState.ContainsKey(string.Empty));
        Assert.Equal("The product API request timed out. Please try again later", _model.ModelState[string.Empty]?.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_SuccessfulApiCall_SetsTempDataAndRedirects()
    {
        // Arrange
        _model.Input = new ProductInput
        {
            Category = "Category1",
            Name = "Product 1",
            Description = "Description 1",
            Price = 10.0m,
            MemberPrice = 8.0m,
            StockQuantity = 50
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Created));

        // Act
        var result = await _model.OnPostAsync(CancellationToken.None);

        // Assert
        var redirectResult = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Officer/Products/Index", redirectResult.PageName);
        Assert.Equal("Product 1 was added successfully", _model.TempData["ProductCreated"]);
    }
}
