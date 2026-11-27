namespace CCOS.Web.Services;

/// <summary>
/// What a cart line holds (CAR-92): a piece of merch, or tickets for an event.
/// </summary>
public enum CartItemKind
{
    /// <summary>Merch from the Merch page.</summary>
    Product,

    /// <summary>Tickets for an event.</summary>
    Ticket,
}

/// <summary>
/// One line in the cart, as kept in the visitor's session (CAR-92). Only ids and the quantity are
/// saved. Names and prices are loaded fresh from the API every time the cart is shown, so a price
/// change, or logging in as a member, shows the right price.
/// </summary>
/// <param name="Kind">Merch or tickets.</param>
/// <param name="ProductId">The merch product id, or for tickets the ticket type id (ticket types are products too).</param>
/// <param name="EventId">The event the tickets are for. Null for merch.</param>
/// <param name="Quantity">How many. Always at least 1.</param>
public sealed record CartItem(CartItemKind Kind, int ProductId, int? EventId, int Quantity)
{
    /// <summary>
    /// Short id for the line, used by the Cart page's Update and Remove buttons: "p3" for merch
    /// product 3, "t12" for ticket type 12.
    /// </summary>
    public string Key => KeyFor(Kind, ProductId);

    /// <summary>The <see cref="Key"/> a line for this item would have.</summary>
    public static string KeyFor(CartItemKind kind, int productId) =>
        (kind == CartItemKind.Ticket ? "t" : "p") + productId.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// What happened when something was added to the cart.
/// </summary>
public enum CartAddResult
{
    /// <summary>Everything asked for was added.</summary>
    Added,

    /// <summary>Only part was added, because the rest would go over the stock or seats left.</summary>
    LimitedToAvailable,

    /// <summary>Nothing was added: none left, or the cart already holds all that's left.</summary>
    NoneAvailable,
}

/// <summary>
/// The visitor's cart (CAR-92): merch and event tickets collected before checkout. Kept in the
/// website session by <see cref="CartSession"/>, so there are no database changes. The rules
/// here (never more than is in stock or seats left, a quantity of 0 removes the line) don't need
/// the API, so they're easy to test.
/// </summary>
public sealed class Cart
{
    private readonly List<CartItem> _items;

    /// <summary>An empty cart.</summary>
    public Cart()
        : this([])
    {
    }

    /// <summary>A cart holding these lines (lines with a quantity under 1 are left out).</summary>
    public Cart(IEnumerable<CartItem> items)
    {
        _items = items.Where(item => item.Quantity > 0).ToList();
    }

    /// <summary>The lines, in the order they were added.</summary>
    public IReadOnlyList<CartItem> Items => _items;

    /// <summary>True when there's nothing in the cart.</summary>
    public bool IsEmpty => _items.Count == 0;

    /// <summary>How many things are in the cart (2 shirts and 3 tickets is 5). Shown next to "Cart" in the menu.</summary>
    public int Count => _items.Sum(item => item.Quantity);

    /// <summary>The line with this <see cref="CartItem.Key"/>, or null.</summary>
    public CartItem? Find(string? key) => _items.FirstOrDefault(item => item.Key == key);

    /// <summary>How many of this product or ticket type are already in the cart.</summary>
    public int QuantityOf(CartItemKind kind, int productId) =>
        Find(CartItem.KeyFor(kind, productId))?.Quantity ?? 0;

    /// <summary>How many tickets for this event are in the cart, across all its ticket types.</summary>
    public int TicketsFor(int eventId) =>
        _items.Where(item => item.Kind == CartItemKind.Ticket && item.EventId == eventId).Sum(item => item.Quantity);

    /// <summary>
    /// The most of one ticket type this cart can hold. Every ticket type shares the event's seats,
    /// so tickets of the event's other types in the cart count against it.
    /// </summary>
    /// <param name="eventId">The event.</param>
    /// <param name="ticketTypeId">The ticket type.</param>
    /// <param name="seatsLeft">Seats still open for the event.</param>
    public int MaxTickets(int eventId, int ticketTypeId, int seatsLeft)
    {
        var otherTypes = TicketsFor(eventId) - QuantityOf(CartItemKind.Ticket, ticketTypeId);
        return Math.Max(0, seatsLeft - otherTypes);
    }

    /// <summary>
    /// Adds merch or tickets. Adding something already in the cart raises its quantity. Never goes
    /// over <paramref name="available"/>; when it would, only what fits is added.
    /// </summary>
    /// <param name="kind">Merch or tickets.</param>
    /// <param name="productId">The product or ticket type id.</param>
    /// <param name="eventId">The event, for tickets. Null for merch.</param>
    /// <param name="quantity">How many to add (at least 1).</param>
    /// <param name="available">The most this line may hold: the stock, or <see cref="MaxTickets"/>.</param>
    public CartAddResult Add(CartItemKind kind, int productId, int? eventId, int quantity, int available)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);

        var current = QuantityOf(kind, productId);
        var room = available - current;
        if (room <= 0)
        {
            return CartAddResult.NoneAvailable;
        }

        var added = Math.Min(quantity, room);
        Put(new CartItem(kind, productId, eventId, current + added));
        return added < quantity ? CartAddResult.LimitedToAvailable : CartAddResult.Added;
    }

    /// <summary>
    /// Sets a line's quantity. 0 or less removes the line; more than <paramref name="available"/>
    /// is lowered to it. Returns false when the line isn't in the cart.
    /// </summary>
    /// <param name="key">The line's <see cref="CartItem.Key"/>.</param>
    /// <param name="quantity">The new quantity.</param>
    /// <param name="available">The most the line may hold.</param>
    public bool SetQuantity(string key, int quantity, int available)
    {
        var item = Find(key);
        if (item is null)
        {
            return false;
        }

        var allowed = Math.Min(quantity, available);
        if (allowed <= 0)
        {
            _items.Remove(item);
        }
        else
        {
            Put(item with { Quantity = allowed });
        }

        return true;
    }

    /// <summary>Takes a line out of the cart. Returns false when it wasn't there.</summary>
    /// <param name="key">The line's <see cref="CartItem.Key"/>.</param>
    public bool Remove(string? key)
    {
        var item = Find(key);
        return item is not null && _items.Remove(item);
    }

    /// <summary>
    /// The price a visitor pays for one: the member price when they can see it and it's lower,
    /// otherwise the regular price. Same rule as the price tags (CAR-78).
    /// </summary>
    /// <param name="price">The regular price.</param>
    /// <param name="memberPrice">The member price, or null when the visitor can't see it.</param>
    public static decimal PriceYouPay(decimal price, decimal? memberPrice) =>
        memberPrice is decimal member && member < price ? member : price;

    // Replaces the line with the same key, keeping its place, or adds it at the end.
    private void Put(CartItem item)
    {
        var index = _items.FindIndex(existing => existing.Key == item.Key);
        if (index >= 0)
        {
            _items[index] = item;
        }
        else
        {
            _items.Add(item);
        }
    }
}
