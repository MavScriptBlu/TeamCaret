using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CCOS.Shared;
using CCOS.Web.Pages.Officer.Products;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Contrib.HttpClient;
using Xunit;

namespace CCOS.Web.Tests.Pages.Officer.Products;

public class CreateModelTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly ProductsApiClient _productsApi;
    private readonly Mock<ILogger<CreateModel>> _loggerMock;
    private readonly CreateModel _model;

    public CreateModelTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = _handlerMock.CreateClient();
        _httpClient.BaseAddress = new Uri("http://localhost");

        _productsApi = new ProductsApiClient(_httpClient);
        _loggerMock = new Mock<ILogger<CreateModel>>();

        _model = new CreateModel(_productsApi, _loggerMock.Object)
        {
            Input = new ProductInput
            {
                Category = "Test Category",
                Name = "Test Name",
                Description = "Test Description",
                Price = 10.0m,
                MemberPrice = 8.0m,
                StockQuantity = 10
            },
            TempData = new Mock<ITempDataDictionary>().Object
        };
    }

    [Fact]
    public async Task OnPostAsync_HttpRequestException_AddsModelError_AndReturnsPage()
    {
        // Arrange
        _handlerMock.SetupAnyRequest().ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _model.OnPostAsync(CancellationToken.None);

        // Assert
        Assert.IsType<PageResult>(result);
        Assert.False(_model.ModelState.IsValid);
        Assert.True(_model.ModelState.ContainsKey(string.Empty));
        Assert.Equal("The product API could not be reached. Please try again later", _model.ModelState[string.Empty]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_TaskCanceledException_Timeout_AddsModelError_AndReturnsPage()
    {
        // Arrange
        _handlerMock.SetupAnyRequest().ThrowsAsync(new TaskCanceledException("Timeout error"));

        // Act
        var result = await _model.OnPostAsync(CancellationToken.None);

        // Assert
        Assert.IsType<PageResult>(result);
        Assert.False(_model.ModelState.IsValid);
        Assert.True(_model.ModelState.ContainsKey(string.Empty));
        Assert.Equal("The product API request timed out. Please try again later", _model.ModelState[string.Empty]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_TaskCanceledException_UserCancelled_ThrowsException()
    {
        // Arrange
        _handlerMock.SetupAnyRequest().ThrowsAsync(new TaskCanceledException("User cancelled"));

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Simulate user cancellation

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => _model.OnPostAsync(cts.Token));
    }

    [Fact]
    public async Task OnPostAsync_ApiReturnsErrorStatusCode_AddsModelError_AndReturnsPage()
    {
        // Arrange
        var errorResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        _handlerMock.SetupAnyRequest().ReturnsAsync(errorResponse);

        // Act
        var result = await _model.OnPostAsync(CancellationToken.None);

        // Assert
        Assert.IsType<PageResult>(result);
        Assert.False(_model.ModelState.IsValid);
        Assert.True(_model.ModelState.ContainsKey(string.Empty));
        Assert.Equal("The product could not be created. Please try again", _model.ModelState[string.Empty]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_InvalidModelState_ReturnsPage_DoesNotCallApi()
    {
        // Arrange
        _model.ModelState.AddModelError("Name", "Name is required");

        // Act
        var result = await _model.OnPostAsync(CancellationToken.None);

        // Assert
        Assert.IsType<PageResult>(result);
        _handlerMock.VerifyAnyRequest(Times.Never());
    }

    [Fact]
    public async Task OnPostAsync_Success_SetsTempData_AndRedirectsToIndex()
    {
        // Arrange
        var successResponse = new HttpResponseMessage(HttpStatusCode.Created);
        _handlerMock.SetupAnyRequest().ReturnsAsync(successResponse);

        var tempDataMock = new Mock<ITempDataDictionary>();
        _model.TempData = tempDataMock.Object;

        // Act
        var result = await _model.OnPostAsync(CancellationToken.None);

        // Assert
        var redirectResult = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Officer/Products/Index", redirectResult.PageName);
        tempDataMock.VerifySet(td => td["ProductCreated"] = $"{_model.Input.Name} was added successfully", Times.Once);
    }
}
