using System.Net;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Pages;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;

namespace CCOS.Web.Tests.Pages;

/// <summary>The Cart page (CAR-92): lines, totals, member prices, Update and Remove.</summary>
public class CartModelTests
{
    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly CartModel _model;

    public CartModelTests()
    {
        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost");
        _model = new CartModel(new ProductsApiClient(httpClient), new EventsApiClient(httpClient), new Mock<ILogger<CartModel>>().Object);
    }

    [Fact]
    public async Task An_empty_cart_shows_no_lines()
    {
        CartTestContext.Attach(_model);

        await _model.OnGetAsync(CancellationToken.None);

        Assert.Empty(_model.Lines);
        Assert.False(_model.LoadFailed);
        Assert.Equal(0m, _model.Total);
    }

    [Fact]
    public async Task Lines_use_member_prices_and_add_up_to_the_order_total()
    {
        CartTestContext.Attach(_model, CartWith(
            new CartItem(CartItemKind.Product, 1, null, 2),
            new CartItem(CartItemKind.Ticket, 12, 5, 3)), member: true);
        RespondWithProduct(new ProductDto(1, "Swag", "Club T-Shirt", null, 20m, 15m, 10, true));
        RespondWithEvent(GameNight(capacity: 50, new TicketTypeDto(12, "General", 5m, null, true)));

        await _model.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, _model.Lines.Count);
        var shirt = _model.Lines[0];
        Assert.Equal("Club T-Shirt", shirt.Name);
        Assert.Equal(15m, shirt.UnitPrice);
        Assert.Equal(30m, shirt.LineTotal);
        Assert.Equal("$15.00", shirt.PriceTag.Member);
        var tickets = _model.Lines[1];
        Assert.Equal("General ticket", tickets.Name);
        Assert.Equal(15m, tickets.LineTotal);
        Assert.Equal(45m, _model.Total);
        Assert.Equal("$45.00", _model.TotalText);
        Assert.Equal(5, _model.ItemCount);
        Assert.Empty(_model.Notices);
    }

    [Fact]
    public async Task A_quantity_over_the_stock_is_lowered_and_saved()
    {
        var http = CartTestContext.Attach(_model, CartWith(new CartItem(CartItemKind.Product, 1, null, 3)));
        RespondWithProduct(new ProductDto(1, "Swag", "Club T-Shirt", null, 20m, null, 1, true));

        await _model.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, Assert.Single(_model.Lines).Quantity);
        Assert.Single(_model.Notices);
        Assert.Equal(1, http.GetCart().QuantityOf(CartItemKind.Product, 1));
    }

    [Fact]
    public async Task A_product_that_is_no_longer_sold_is_taken_out()
    {
        var http = CartTestContext.Attach(_model, CartWith(new CartItem(CartItemKind.Product, 1, null, 2)));
        _mockHttp.When(HttpMethod.Get, "http://localhost/api/products/1").Respond(HttpStatusCode.NotFound);

        await _model.OnGetAsync(CancellationToken.None);

        Assert.Empty(_model.Lines);
        Assert.Single(_model.Notices);
        Assert.True(http.GetCart().IsEmpty);
    }

    [Fact]
    public async Task Members_only_tickets_are_taken_out_for_a_visitor_who_is_not_a_member()
    {
        var http = CartTestContext.Attach(_model, CartWith(new CartItem(CartItemKind.Ticket, 12, 5, 1)));
        RespondWithEvent(GameNight(capacity: 50, new TicketTypeDto(12, "Members", 0m, null, true)) with { MembersOnly = true });

        await _model.OnGetAsync(CancellationToken.None);

        Assert.Empty(_model.Lines);
        Assert.True(http.GetCart().IsEmpty);
    }

    [Fact]
    public async Task The_page_says_so_when_the_API_is_down_and_keeps_the_cart()
    {
        var http = CartTestContext.Attach(_model, CartWith(new CartItem(CartItemKind.Product, 1, null, 2)));
        _mockHttp.When(HttpMethod.Get, "http://localhost/api/products/1").Throw(new HttpRequestException("down"));

        await _model.OnGetAsync(CancellationToken.None);

        Assert.True(_model.LoadFailed);
        Assert.Equal(2, http.GetCart().Count);
    }

    [Fact]
    public async Task Update_changes_the_quantity()
    {
        var http = CartTestContext.Attach(_model, CartWith(new CartItem(CartItemKind.Product, 1, null, 1)));
        RespondWithProduct(new ProductDto(1, "Swag", "Club T-Shirt", null, 20m, null, 10, true));

        var result = await _model.OnPostUpdateAsync("p1", 4, CancellationToken.None);

        Assert.Equal("/Cart", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.Equal(4, http.GetCart().QuantityOf(CartItemKind.Product, 1));
        Assert.Equal("Cart updated.", _model.TempData[CartMessages.MessageKey]);
    }

    [Fact]
    public async Task Update_lowers_a_quantity_over_the_stock()
    {
        var http = CartTestContext.Attach(_model, CartWith(new CartItem(CartItemKind.Product, 1, null, 1)));
        RespondWithProduct(new ProductDto(1, "Swag", "Club T-Shirt", null, 20m, null, 3, true));

        await _model.OnPostUpdateAsync("p1", 8, CancellationToken.None);

        Assert.Equal(3, http.GetCart().QuantityOf(CartItemKind.Product, 1));
    }

    [Fact]
    public async Task Update_to_zero_removes_the_line()
    {
        var http = CartTestContext.Attach(_model, CartWith(new CartItem(CartItemKind.Product, 1, null, 2)));

        await _model.OnPostUpdateAsync("p1", 0, CancellationToken.None);

        Assert.True(http.GetCart().IsEmpty);
    }

    [Fact]
    public void Remove_takes_the_line_out()
    {
        var http = CartTestContext.Attach(_model, CartWith(
            new CartItem(CartItemKind.Product, 1, null, 2),
            new CartItem(CartItemKind.Ticket, 12, 5, 1)));

        var result = _model.OnPostRemove("p1");

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("t12", Assert.Single(http.GetCart().Items).Key);
    }

    [Fact]
    public void Remove_of_something_not_in_the_cart_says_so()
    {
        CartTestContext.Attach(_model);

        _model.OnPostRemove("p99");

        Assert.NotNull(_model.TempData[CartMessages.ErrorKey]);
    }

    private static Cart CartWith(params CartItem[] items) => new(items);

    private void RespondWithProduct(ProductDto product) =>
        _mockHttp.When(HttpMethod.Get, $"http://localhost/api/products/{product.ProductId}")
            .Respond("application/json", JsonSerializer.Serialize(product));

    private void RespondWithEvent(EventDto dto) =>
        _mockHttp.When(HttpMethod.Get, $"http://localhost/api/events/{dto.EventId}")
            .Respond("application/json", JsonSerializer.Serialize(dto));

    internal static EventDto GameNight(int capacity, params TicketTypeDto[] ticketTypes) => new(
        5, "Game Night", null,
        DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(7).AddHours(3),
        capacity, false, true, 1, "Student Union", ticketTypes, 1);
}
