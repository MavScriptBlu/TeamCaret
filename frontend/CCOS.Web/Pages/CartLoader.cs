using CCOS.Web.Pages.Events;
using CCOS.Web.Services;

namespace CCOS.Web.Pages;

/// <summary>
/// Loads the cart's names, prices and stock fresh from the API, for the Cart page (CAR-92) and
/// Checkout (CAR-93). The cart only holds ids and quantities, so this is where each line gets its
/// price (member price for current members) and how many are available. Anything no longer for
/// sale is taken out of the cart, and quantities over the stock or seats left are lowered, with a
/// note. API errors (<see cref="HttpRequestException"/> and friends) are left for the page to catch.
/// </summary>
public sealed class CartLoader(ProductsApiClient productsApi, EventsApiClient eventsApi)
{
    /// <summary>
    /// Loads every line of the cart. Changes the cart when something had to be taken out or
    /// lowered (the result says so, and the page saves it).
    /// </summary>
    /// <param name="cart">The visitor's cart.</param>
    /// <param name="isCurrentMember">True for a current member (member prices, members-only tickets).</param>
    /// <param name="cancellationToken">Stops the API calls when the visitor leaves.</param>
    public async Task<CartLoadResult> LoadAsync(Cart cart, bool isCurrentMember, CancellationToken cancellationToken)
    {
        var lines = new List<CartLine>();
        var notices = new List<string>();
        var events = new Dictionary<int, EventInfo?>();
        var changed = false;

        foreach (var item in cart.Items.ToList())
        {
            var found = await LookUpAsync(cart, item, isCurrentMember, events, cancellationToken);
            if (found.Problem is not null)
            {
                cart.Remove(item.Key);
                notices.Add(found.Problem);
                changed = true;
                continue;
            }

            var quantity = item.Quantity;
            if (quantity > found.Available)
            {
                quantity = found.Available;
                cart.SetQuantity(item.Key, quantity, found.Available);
                notices.Add(found.LoweredNotice());
                changed = true;
            }

            lines.Add(new CartLine(item.Key, found.Name, found.Detail, found.Href, found.PriceTag!, found.UnitPrice, quantity, found.Available));
        }

        return new CartLoadResult(lines, notices, changed);
    }

    /// <summary>
    /// Loads one cart item's name, price and how many are available. Problem is set instead when
    /// the item can't stay in the cart (no longer sold, sold out, event started, members only).
    /// </summary>
    /// <param name="cart">The cart the item is in (tickets share their event's seats).</param>
    /// <param name="item">The cart line to look up.</param>
    /// <param name="isCurrentMember">True for a current member.</param>
    /// <param name="events">Events already loaded for this cart, so each event is fetched once.</param>
    /// <param name="cancellationToken">Stops the API calls when the visitor leaves.</param>
    public async Task<CartItemLookUp> LookUpAsync(Cart cart, CartItem item, bool isCurrentMember, Dictionary<int, EventInfo?> events, CancellationToken cancellationToken)
    {
        var found = await FindAsync(cart, item, isCurrentMember, events, cancellationToken);
        return found.Problem is null && found.Available <= 0
            ? found with { Problem = $"{found.Name} is no longer available, so it was taken out of your cart." }
            : found;
    }

    private async Task<CartItemLookUp> FindAsync(Cart cart, CartItem item, bool isCurrentMember, Dictionary<int, EventInfo?> events, CancellationToken cancellationToken)
    {
        if (item.Kind == CartItemKind.Product)
        {
            var product = await productsApi.GetAsync(item.ProductId, cancellationToken);
            if (product is null)
            {
                return CartItemLookUp.Gone("An item in your cart is no longer sold, so it was taken out.");
            }

            if (product.StockQuantity <= 0)
            {
                return CartItemLookUp.Gone($"{product.Name} is sold out, so it was taken out of your cart.");
            }

            return new CartItemLookUp(
                product.Name,
                product.Category,
                $"/Products/Details/{product.ProductId}",
                PriceTag.ForProduct(product.Price, product.MemberPrice),
                Cart.PriceYouPay(product.Price, product.MemberPrice),
                product.StockQuantity);
        }

        if (item.EventId is not int eventId)
        {
            return CartItemLookUp.Gone("An item in your cart is no longer sold, so it was taken out.");
        }

        if (!events.TryGetValue(eventId, out var eventInfo))
        {
            var dto = await eventsApi.GetAsync(eventId, cancellationToken);
            eventInfo = dto is null ? null : EventMapping.ToEventInfo(dto);
            events[eventId] = eventInfo;
        }

        var ticketType = eventInfo?.TicketTypes.FirstOrDefault(type => type.IsActive && type.TicketTypeId == item.ProductId);
        if (eventInfo is null || !eventInfo.IsActive || ticketType is null)
        {
            return CartItemLookUp.Gone("Tickets for an event in your cart are no longer sold, so they were taken out.");
        }

        var reason = DetailsModel.WhyNoTickets(eventInfo, isCurrentMember, DateTime.UtcNow);
        if (reason is not null)
        {
            return CartItemLookUp.Gone($"{eventInfo.Name} tickets were taken out of your cart. {reason}");
        }

        return new CartItemLookUp(
            $"{ticketType.Name} ticket",
            $"{eventInfo.Name} · {ClubTime.FormatCard(eventInfo.StartsAtUtc)} · {eventInfo.VenueName}",
            $"/Events/Details/{eventInfo.EventId}",
            PriceTag.ForProduct(ticketType.Price, ticketType.MemberPrice),
            Cart.PriceYouPay(ticketType.Price, ticketType.MemberPrice),
            // The website only knows the event's capacity. The order API (CAR-91) checks the real
            // seats left when the order is placed and names the event if there aren't enough.
            cart.MaxTickets(eventId, item.ProductId, eventInfo.TicketCapacity));
    }
}

/// <summary>
/// The cart, loaded and checked (CAR-92, CAR-93).
/// </summary>
/// <param name="Lines">The lines that can stay in the cart, ready to show.</param>
/// <param name="Notices">What was taken out or lowered, in plain sentences.</param>
/// <param name="Changed">True when the cart was changed and needs saving.</param>
public sealed record CartLoadResult(IReadOnlyList<CartLine> Lines, IReadOnlyList<string> Notices, bool Changed)
{
    /// <summary>The order total: every line total added up.</summary>
    public decimal Total => Lines.Sum(line => line.LineTotal);
}

/// <summary>
/// What the API says about one cart item: its name, price and how many are available, or the
/// Problem that means it has to come out of the cart.
/// </summary>
/// <param name="Name">What it is, for example "Club T-Shirt" or "General ticket".</param>
/// <param name="Detail">The product's category, or the event's name, date and venue.</param>
/// <param name="Href">Link to the product or event page.</param>
/// <param name="PriceTag">The price for one, for _PriceTag. Null only when Problem is set.</param>
/// <param name="UnitPrice">The price the visitor pays for one.</param>
/// <param name="Available">The most the line can hold (stock or seats left).</param>
/// <param name="Problem">Why it can't stay in the cart, or null.</param>
public sealed record CartItemLookUp(string Name, string Detail, string Href, PriceTag? PriceTag, decimal UnitPrice, int Available, string? Problem = null)
{
    /// <summary>An item that has to come out of the cart, and why.</summary>
    public static CartItemLookUp Gone(string problem) => new("", "", "", null, 0, 0, problem);

    /// <summary>The note shown when a quantity is lowered to what's available.</summary>
    public string LoweredNotice() => $"Only {Available} {Name} available, so your cart now has {Available}.";
}

/// <summary>
/// One line on the Cart and Checkout pages (CAR-92, CAR-93).
/// </summary>
/// <param name="Key">The line's key, sent by its Update and Remove buttons.</param>
/// <param name="Name">What it is, for example "Club T-Shirt" or "General ticket".</param>
/// <param name="Detail">The product's category, or the event's name, date and venue.</param>
/// <param name="Href">Link to the product or event page.</param>
/// <param name="PriceTag">The price for one, drawn by _PriceTag (member price for members).</param>
/// <param name="UnitPrice">The price the visitor pays for one.</param>
/// <param name="Quantity">How many.</param>
/// <param name="MaxQuantity">The most this line can hold (stock or seats left).</param>
public sealed record CartLine(string Key, string Name, string Detail, string Href, PriceTag PriceTag, decimal UnitPrice, int Quantity, int MaxQuantity)
{
    /// <summary>Price for one times the quantity.</summary>
    public decimal LineTotal => UnitPrice * Quantity;

    /// <summary>The line total as text, for example "$30.00" or "Free".</summary>
    public string LineTotalText => Money.Format(LineTotal);
}
