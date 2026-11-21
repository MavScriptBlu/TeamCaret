using System.Text.Json;
using CCOS.Web.Pages.Events;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages;

/// <summary>
/// The Cart page (/Cart, CAR-92): every item with its price, quantity and line total, plus the
/// order total. The cart itself only holds ids and quantities (see <see cref="CartSession"/>), so
/// this page loads names, prices and stock fresh from the API each time. Anything that's no longer
/// available is taken out, and quantities over the stock or seats left are lowered, with a note.
/// </summary>
public class CartModel(ProductsApiClient productsApi, EventsApiClient eventsApi, ILogger<CartModel> logger) : PageModel
{
    private const string ItemGone = "That item isn't in your cart anymore.";
    private const string ItemRemoved = "Item removed from your cart.";

    /// <summary>The cart's lines, ready to show. Empty when the cart is empty.</summary>
    public IReadOnlyList<CartLine> Lines { get; private set; } = [];

    /// <summary>
    /// Notes about changes made while loading the cart, for example
    /// "Only 2 Club T-Shirt left, so your cart now has 2."
    /// </summary>
    public IReadOnlyList<string> Notices { get; private set; } = [];

    /// <summary>The order total: every line total added up.</summary>
    public decimal Total => Lines.Sum(line => line.LineTotal);

    /// <summary>The order total as text, for example "$45.00".</summary>
    public string TotalText => Money.Format(Total);

    /// <summary>How many things are in the cart.</summary>
    public int ItemCount => Lines.Sum(line => line.Quantity);

    /// <summary>True when the API couldn't be reached, so the page shows an error message instead.</summary>
    public bool LoadFailed { get; private set; }

    /// <summary>
    /// Loads the cart's items from the API and works out the totals. Saves the cart again when
    /// something had to be taken out or lowered.
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var cart = HttpContext.GetCart();
        if (cart.IsEmpty)
        {
            return;
        }

        var lines = new List<CartLine>();
        var notices = new List<string>();
        var events = new Dictionary<int, EventInfo?>();
        var changed = false;

        try
        {
            foreach (var item in cart.Items.ToList())
            {
                var found = await LookUpAsync(cart, item, events, cancellationToken);
                if (found.Problem is null && found.Available <= 0)
                {
                    found = found with { Problem = $"{found.Name} is no longer available, so it was taken out of your cart." };
                }

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
                    notices.Add($"Only {found.Available} {found.Name} available, so your cart now has {found.Available}.");
                    changed = true;
                }

                lines.Add(new CartLine(item.Key, found.Name, found.Detail, found.Href, found.PriceTag!, found.UnitPrice, quantity, found.Available));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't load the cart's items from CCOS.Api.");
            LoadFailed = true;
            return;
        }

        if (changed)
        {
            HttpContext.SaveCart(cart);
        }

        Lines = lines;
        Notices = notices;
    }

    /// <summary>
    /// The Update button on a line: sets its quantity. 0 removes it, and more than is available
    /// is lowered to what's available. Stock and seats are checked with the API first.
    /// </summary>
    /// <param name="key">The line's key, for example "p3".</param>
    /// <param name="quantity">The new quantity.</param>
    public async Task<IActionResult> OnPostUpdateAsync(string? key, int quantity, CancellationToken cancellationToken)
    {
        var cart = HttpContext.GetCart();
        var item = cart.Find(key);
        if (item is null)
        {
            TempData[CartMessages.ErrorKey] = ItemGone;
            return RedirectToPage("/Cart");
        }

        if (quantity <= 0)
        {
            cart.Remove(item.Key);
            HttpContext.SaveCart(cart);
            TempData[CartMessages.MessageKey] = ItemRemoved;
            return RedirectToPage("/Cart");
        }

        LookUp found;
        try
        {
            found = await LookUpAsync(cart, item, new Dictionary<int, EventInfo?>(), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't check cart item {CartKey} with CCOS.Api.", item.Key);
            TempData[CartMessages.ErrorKey] = CartMessages.TryAgainLater;
            return RedirectToPage("/Cart");
        }

        if (found.Problem is null && found.Available <= 0)
        {
            found = found with { Problem = $"{found.Name} is no longer available, so it was taken out of your cart." };
        }

        if (found.Problem is not null)
        {
            cart.Remove(item.Key);
            TempData[CartMessages.ErrorKey] = found.Problem;
        }
        else
        {
            cart.SetQuantity(item.Key, quantity, found.Available);
            TempData[CartMessages.MessageKey] = quantity > found.Available
                ? $"Only {found.Available} {found.Name} available, so your cart now has {found.Available}."
                : "Cart updated.";
        }

        HttpContext.SaveCart(cart);
        return RedirectToPage("/Cart");
    }

    /// <summary>The Remove button on a line: takes it out of the cart.</summary>
    /// <param name="key">The line's key, for example "t12".</param>
    public IActionResult OnPostRemove(string? key)
    {
        var cart = HttpContext.GetCart();
        if (cart.Remove(key))
        {
            HttpContext.SaveCart(cart);
            TempData[CartMessages.MessageKey] = ItemRemoved;
        }
        else
        {
            TempData[CartMessages.ErrorKey] = ItemGone;
        }

        return RedirectToPage("/Cart");
    }

    // Loads one cart item's name, price and how many are available from the API. Problem is set
    // instead when the item can't stay in the cart (removed, out of stock, event started, ...).
    private async Task<LookUp> LookUpAsync(Cart cart, CartItem item, Dictionary<int, EventInfo?> events, CancellationToken cancellationToken)
    {
        if (item.Kind == CartItemKind.Product)
        {
            var product = await productsApi.GetAsync(item.ProductId, cancellationToken);
            if (product is null)
            {
                return LookUp.Gone("An item in your cart is no longer sold, so it was taken out.");
            }

            if (product.StockQuantity <= 0)
            {
                return LookUp.Gone($"{product.Name} is sold out, so it was taken out of your cart.");
            }

            return new LookUp(
                product.Name,
                product.Category,
                $"/Products/Details/{product.ProductId}",
                PriceTag.ForProduct(product.Price, product.MemberPrice),
                Cart.PriceYouPay(product.Price, product.MemberPrice),
                product.StockQuantity);
        }

        if (item.EventId is not int eventId)
        {
            return LookUp.Gone("An item in your cart is no longer sold, so it was taken out.");
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
            return LookUp.Gone("Tickets for an event in your cart are no longer sold, so they were taken out.");
        }

        var reason = DetailsModel.WhyNoTickets(eventInfo, User.IsCurrentMember(), DateTime.UtcNow);
        if (reason is not null)
        {
            return LookUp.Gone($"{eventInfo.Name} tickets were taken out of your cart. {reason}");
        }

        return new LookUp(
            $"{ticketType.Name} ticket",
            $"{eventInfo.Name} · {ClubTime.FormatCard(eventInfo.StartsAtUtc)} · {eventInfo.VenueName}",
            $"/Events/Details/{eventInfo.EventId}",
            PriceTag.ForProduct(ticketType.Price, ticketType.MemberPrice),
            Cart.PriceYouPay(ticketType.Price, ticketType.MemberPrice),
            // Seats left is the event's capacity for now; ordered tickets will count once orders exist (CAR-91).
            cart.MaxTickets(eventId, item.ProductId, eventInfo.TicketCapacity));
    }

    // What the API says about one cart item.
    private sealed record LookUp(string Name, string Detail, string Href, PriceTag? PriceTag, decimal UnitPrice, int Available, string? Problem = null)
    {
        public static LookUp Gone(string problem) => new("", "", "", null, 0, 0, problem);
    }
}

/// <summary>
/// One line on the Cart page (CAR-92).
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
