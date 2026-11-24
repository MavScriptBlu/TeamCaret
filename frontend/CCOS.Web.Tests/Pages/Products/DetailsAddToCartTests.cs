using System.Net;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Pages.Products;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;

namespace CCOS.Web.Tests.Pages.Products;

/// <summary>The Add to cart button on the product page (CAR-92).</summary>
public class DetailsAddToCartTests
{
    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly DetailsModel _model;

    public DetailsAddToCartTests()
    {
        HttpClient httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost");
        _model = new DetailsModel(new ProductsApiClient(httpClient), new Mock<ILogger<DetailsModel>>().Object);
    }

    [Fact]
    public async Task Add_to_cart_adds_the_product_and_comes_back_with_a_message()
    {
        HttpContext http = CartTestContext.Attach(_model);
        RespondWith(stock: 10);

        IActionResult result = await _model.OnPostAddToCartAsync(1, 2, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Products/Details", redirect.PageName);
        Assert.Equal(2, http.GetCart().QuantityOf(CartItemKind.Product, 1));
        Assert.Equal("Added 2 × Club T-Shirt to your cart.", _model.TempData[CartMessages.MessageKey]);
    }

    [Fact]
    public async Task Add_to_cart_never_adds_more_than_the_stock()
    {
        HttpContext http = CartTestContext.Attach(_model);
        RespondWith(stock: 3);

        await _model.OnPostAddToCartAsync(1, 5, CancellationToken.None);

        Assert.Equal(3, http.GetCart().QuantityOf(CartItemKind.Product, 1));
    }

    [Fact]
    public async Task An_out_of_stock_product_cannot_be_added()
    {
        HttpContext http = CartTestContext.Attach(_model);
        RespondWith(stock: 0);

        await _model.OnPostAddToCartAsync(1, 1, CancellationToken.None);

        Assert.True(http.GetCart().IsEmpty);
        Assert.Equal("Club T-Shirt is sold out.", _model.TempData[CartMessages.ErrorKey]);
    }

    [Fact]
    public async Task A_quantity_of_zero_is_refused()
    {
        HttpContext http = CartTestContext.Attach(_model);
        RespondWith(stock: 10);

        await _model.OnPostAddToCartAsync(1, 0, CancellationToken.None);

        Assert.True(http.GetCart().IsEmpty);
        Assert.Equal(CartMessages.PickAQuantity, _model.TempData[CartMessages.ErrorKey]);
    }

    [Fact]
    public async Task An_unknown_product_is_not_found()
    {
        CartTestContext.Attach(_model);
        _mockHttp.When(HttpMethod.Get, "http://localhost/api/products/1").Respond(HttpStatusCode.NotFound);

        IActionResult result = await _model.OnPostAddToCartAsync(1, 1, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private void RespondWith(int stock) =>
        _mockHttp.When(HttpMethod.Get, "http://localhost/api/products/1")
            .Respond("application/json", JsonSerializer.Serialize(new ProductDto(1, "Swag", "Club T-Shirt", null, 20m, null, stock, true)));
}
