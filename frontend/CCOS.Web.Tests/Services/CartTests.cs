using CCOS.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace CCOS.Web.Tests.Services;

/// <summary>The cart's rules (CAR-92): adding, changing quantity, removing, stock and seat limits.</summary>
public class CartTests
{
    [Fact]
    public void Add_puts_a_new_line_in_the_cart()
    {
        var cart = new Cart();

        var result = cart.Add(CartItemKind.Product, 3, null, 2, available: 10);

        Assert.Equal(CartAddResult.Added, result);
        var item = Assert.Single(cart.Items);
        Assert.Equal("p3", item.Key);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public void Adding_the_same_item_again_raises_its_quantity()
    {
        var cart = new Cart();
        cart.Add(CartItemKind.Product, 3, null, 2, available: 10);

        cart.Add(CartItemKind.Product, 3, null, 3, available: 10);

        Assert.Equal(5, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public void Add_never_goes_over_the_stock()
    {
        var cart = new Cart();
        cart.Add(CartItemKind.Product, 3, null, 2, available: 4);

        var result = cart.Add(CartItemKind.Product, 3, null, 5, available: 4);

        Assert.Equal(CartAddResult.LimitedToAvailable, result);
        Assert.Equal(4, cart.QuantityOf(CartItemKind.Product, 3));
    }

    [Fact]
    public void Add_adds_nothing_when_out_of_stock()
    {
        var cart = new Cart();

        var result = cart.Add(CartItemKind.Product, 3, null, 1, available: 0);

        Assert.Equal(CartAddResult.NoneAvailable, result);
        Assert.True(cart.IsEmpty);
    }

    [Fact]
    public void Add_adds_nothing_when_the_cart_already_holds_all_the_stock()
    {
        var cart = new Cart();
        cart.Add(CartItemKind.Product, 3, null, 4, available: 4);

        var result = cart.Add(CartItemKind.Product, 3, null, 1, available: 4);

        Assert.Equal(CartAddResult.NoneAvailable, result);
        Assert.Equal(4, cart.Count);
    }

    [Fact]
    public void Merch_and_tickets_with_the_same_id_are_separate_lines()
    {
        var cart = new Cart();
        cart.Add(CartItemKind.Product, 7, null, 1, available: 5);
        cart.Add(CartItemKind.Ticket, 7, 2, 2, available: 5);

        Assert.Equal(2, cart.Items.Count);
        Assert.Equal(3, cart.Count);
    }

    [Fact]
    public void SetQuantity_changes_the_quantity_and_lowers_it_to_the_stock()
    {
        var cart = new Cart([new CartItem(CartItemKind.Product, 3, null, 2)]);

        cart.SetQuantity("p3", 9, available: 6);

        Assert.Equal(6, cart.QuantityOf(CartItemKind.Product, 3));
    }

    [Fact]
    public void SetQuantity_of_zero_removes_the_line()
    {
        var cart = new Cart([new CartItem(CartItemKind.Product, 3, null, 2)]);

        Assert.True(cart.SetQuantity("p3", 0, available: 6));

        Assert.True(cart.IsEmpty);
    }

    [Fact]
    public void Remove_takes_the_line_out()
    {
        var cart = new Cart([new CartItem(CartItemKind.Product, 3, null, 2), new CartItem(CartItemKind.Ticket, 12, 5, 1)]);

        Assert.True(cart.Remove("p3"));
        Assert.False(cart.Remove("p3"));

        Assert.Equal("t12", Assert.Single(cart.Items).Key);
    }

    [Fact]
    public void MaxTickets_counts_the_events_other_ticket_types()
    {
        // 10 seats: 3 General tickets already in the cart leave room for 7 Student tickets.
        var cart = new Cart([new CartItem(CartItemKind.Ticket, 12, 5, 3), new CartItem(CartItemKind.Ticket, 13, 5, 2)]);

        Assert.Equal(7, cart.MaxTickets(eventId: 5, ticketTypeId: 13, seatsLeft: 10));
        Assert.Equal(5, cart.TicketsFor(5));
    }

    [Theory]
    [InlineData(20, 15, 15)]
    [InlineData(20, null, 20)]
    [InlineData(20, 25, 20)]
    [InlineData(5, 0, 0)]
    public void PriceYouPay_is_the_lower_member_price_when_there_is_one(int price, int? memberPrice, int expected)
    {
        Assert.Equal(expected, Cart.PriceYouPay(price, memberPrice));
    }

    [Fact]
    public void The_cart_is_saved_in_and_read_back_from_the_session()
    {
        var http = NewContextWithSession();
        var cart = new Cart();
        cart.Add(CartItemKind.Product, 3, null, 2, available: 10);
        cart.Add(CartItemKind.Ticket, 12, 5, 1, available: 10);

        http.SaveCart(cart);
        var loaded = http.GetCart();

        Assert.Equal(cart.Items, loaded.Items);
    }

    [Fact]
    public void GetCart_is_empty_without_a_session_or_with_a_broken_cart()
    {
        Assert.True(((HttpContext?)null).GetCart().IsEmpty);
        Assert.True(new DefaultHttpContext().GetCart().IsEmpty);

        var http = NewContextWithSession();
        http.Session.SetString(CartSession.Key, "not json");
        Assert.True(http.GetCart().IsEmpty);
    }

    private static DefaultHttpContext NewContextWithSession()
    {
        var http = new DefaultHttpContext();
        http.Features.Set<ISessionFeature>(new Feature { Session = new TestSession() });
        return http;
    }

    private sealed class Feature : ISessionFeature
    {
        public ISession Session { get; set; } = default!;
    }
}
