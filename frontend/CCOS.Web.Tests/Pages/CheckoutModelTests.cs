using System.Net;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Pages;
using CCOS.Web.Pages.Checkout;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;

namespace CCOS.Web.Tests.Pages;

/// <summary>Checkout (CAR-93): review, place the order, sold out, API down, and the confirmation page.</summary>
public class CheckoutModelTests
{
    private const string OrdersUrl = "http://localhost/api/orders";

    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly CheckoutModel _model;

    public CheckoutModelTests()
    {
        HttpClient httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost");
        _model = new CheckoutModel(
            new ProductsApiClient(httpClient),
            new EventsApiClient(httpClient),
            new OrdersApiClient(httpClient),
            new Mock<ILogger<CheckoutModel>>().Object);
    }

    [Fact]
    public async Task An_empty_cart_goes_back_to_the_cart_page()
    {
        CartTestContext.Attach(_model);

        IActionResult result = await _model.OnGetAsync(CancellationToken.None);

        Assert.Equal("/Cart", Assert.IsType<RedirectToPageResult>(result).PageName);
    }

    [Fact]
    public async Task The_review_shows_the_items_and_the_total()
    {
        CartTestContext.Attach(_model, Shirts(2), member: true);
        RespondWithShirt(stock: 10);

        IActionResult result = await _model.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        CartLine line = Assert.Single(_model.Lines);
        Assert.Equal("Club T-Shirt", line.Name);
        Assert.Equal(24m, _model.Total);
        Assert.Equal("$24.00", _model.TotalText);
    }

    [Fact]
    public async Task Placing_the_order_empties_the_cart_and_shows_the_confirmation()
    {
        HttpContext http = CartTestContext.Attach(_model, Shirts(2), member: true);
        RespondWithShirt(stock: 10);
        OrderDto placed = new(41, DateTime.UtcNow, "Pending", 24m, [new OrderLineDto(1, "Club T-Shirt", null, 2, 12m, 24m)]);
        MockedRequest post = _mockHttp.When(HttpMethod.Post, OrdersUrl)
            .WithPartialContent("\"productId\":1,\"quantity\":2");
        post.Respond(HttpStatusCode.Created, "application/json", JsonSerializer.Serialize(placed));

        IActionResult result = await _model.OnPostAsync(CancellationToken.None);

        Assert.Equal("/Checkout/Confirmation", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.Equal(1, _mockHttp.GetMatchCount(post));
        Assert.True(http.GetCart().IsEmpty);
        OrderDto? handedOver = JsonSerializer.Deserialize<OrderDto>((string)_model.TempData[CheckoutModel.PlacedOrderKey]!);
        Assert.Equal(41, handedOver?.OrderId);
    }

    [Fact]
    public async Task A_sold_out_item_keeps_the_cart_and_says_which_item_to_change()
    {
        HttpContext http = CartTestContext.Attach(_model, Shirts(2));
        RespondWithShirt(stock: 10);
        _mockHttp.When(HttpMethod.Post, OrdersUrl).Respond(HttpStatusCode.Conflict, "application/problem+json",
            """{"title":"Not available","status":409,"detail":"Club T-Shirt doesn't have 2 left in stock.","productId":1}""");

        IActionResult result = await _model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Club T-Shirt doesn't have 2 left in stock. Change it in your cart, then try again.", _model.ErrorMessage);
        Assert.Equal("p1", _model.ProblemKey);
        Assert.Equal(2, http.GetCart().QuantityOf(CartItemKind.Product, 1));
    }

    [Fact]
    public async Task When_the_API_is_down_nothing_is_lost_from_the_cart()
    {
        HttpContext http = CartTestContext.Attach(_model, Shirts(2));
        RespondWithShirt(stock: 10);
        _mockHttp.When(HttpMethod.Post, OrdersUrl).Throw(new HttpRequestException("down"));

        IActionResult result = await _model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(CheckoutModel.CouldNotPlace, _model.ErrorMessage);
        Assert.Equal(2, http.GetCart().QuantityOf(CartItemKind.Product, 1));
    }

    [Fact]
    public async Task A_cart_that_changed_is_shown_again_instead_of_ordered()
    {
        // Only 3 left, but the cart asks for 5: the cart is lowered and nothing is ordered yet.
        HttpContext http = CartTestContext.Attach(_model, Shirts(5));
        RespondWithShirt(stock: 3);
        MockedRequest post = _mockHttp.When(HttpMethod.Post, OrdersUrl);
        post.Respond(HttpStatusCode.Created);

        IActionResult result = await _model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(CheckoutModel.CartChanged, _model.ErrorMessage);
        Assert.Single(_model.Notices);
        Assert.Equal(3, http.GetCart().QuantityOf(CartItemKind.Product, 1));
        Assert.Equal(0, _mockHttp.GetMatchCount(post));
    }

    [Fact]
    public async Task An_expired_sign_in_goes_to_log_in_and_back()
    {
        CartTestContext.Attach(_model, Shirts(1));
        RespondWithShirt(stock: 10);
        _mockHttp.When(HttpMethod.Post, OrdersUrl).Respond(HttpStatusCode.Unauthorized);

        IActionResult result = await _model.OnPostAsync(CancellationToken.None);

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Account/Login", redirect.PageName);
        Assert.Equal("/Checkout", redirect.RouteValues?["returnUrl"]);
    }

    [Theory]
    [InlineData("CTF Night doesn't have enough seats left for this order.", "General ticket", "CTF Night doesn't have enough seats left for this order. Change it in your cart, then try again.")]
    [InlineData("Item 7 isn't for sale anymore. Take it out of your cart and try again.", "Club Hoodie", "Club Hoodie isn't available anymore. Change it in your cart, then try again.")]
    [InlineData(null, null, "Something in your cart isn't available anymore. Check your cart, then try again.")]
    public void ProblemMessage_names_the_item(string? apiMessage, string? lineName, string expected)
    {
        Assert.Equal(expected, CheckoutModel.ProblemMessage(apiMessage, lineName));
    }

    [Fact]
    public void The_confirmation_page_shows_the_order_handed_over_by_checkout()
    {
        ConfirmationModel confirmation = new();
        DefaultHttpContext http = new();
        confirmation.PageContext = new PageContext { HttpContext = http };
        confirmation.TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
        {
            [CheckoutModel.PlacedOrderKey] = JsonSerializer.Serialize(new OrderDto(41, DateTime.UtcNow, "Pending", 5m, [])),
        };

        confirmation.OnGet();

        Assert.Equal(41, confirmation.Order?.OrderId);
    }

    [Fact]
    public void The_confirmation_page_opened_again_later_shows_no_order()
    {
        ConfirmationModel confirmation = new();
        DefaultHttpContext http = new();
        confirmation.PageContext = new PageContext { HttpContext = http };
        confirmation.TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>());

        confirmation.OnGet();

        Assert.Null(confirmation.Order);
    }

    private static Cart Shirts(int quantity) => new([new CartItem(CartItemKind.Product, 1, null, quantity)]);

    private void RespondWithShirt(int stock) =>
        _mockHttp.When(HttpMethod.Get, "http://localhost/api/products/1")
            .Respond("application/json", JsonSerializer.Serialize(new ProductDto(1, "Swag", "Club T-Shirt", null, 15m, 12m, stock, true)));
}
