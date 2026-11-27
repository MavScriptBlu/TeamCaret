using System.Text.Json;
using CCOS.Web.Pages.Events;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages;

/// <summary>
/// The Cart page (/Cart, CAR-92): every item with its price, quantity and line total, plus the
/// order total and the Checkout button (CAR-93). The cart itself only holds ids and quantities
/// (see <see cref="CartSession"/>), so <see cref="CartLoader"/> loads names, prices and stock fresh
/// from the API each time. Anything that's no longer available is taken out, and quantities over
/// the stock or seats left are lowered, with a note.
/// </summary>
public class CartModel(ProductsApiClient productsApi, EventsApiClient eventsApi, ILogger<CartModel> logger) : PageModel
{
    private const string ItemGone = "That item isn't in your cart anymore.";
    private const string ItemRemoved = "Item removed from your cart.";

    private readonly CartLoader _loader = new(productsApi, eventsApi);

    /// <summary>The cart's lines, ready to show. Empty when the cart is empty.</summary>
    public IReadOnlyList<CartLine> Lines { get; private set; } = [];

    /// <summary>
    /// Notes about changes made while loading the cart, for example
    /// "Only 2 Club T-Shirt available, so your cart now has 2."
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

        CartLoadResult loaded;
        try
        {
            loaded = await _loader.LoadAsync(cart, User.IsCurrentMember(), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't load the cart's items from CCOS.Api.");
            LoadFailed = true;
            return;
        }

        if (loaded.Changed)
        {
            HttpContext.SaveCart(cart);
        }

        Lines = loaded.Lines;
        Notices = loaded.Notices;
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

        CartItemLookUp found;
        try
        {
            found = await _loader.LookUpAsync(cart, item, User.IsCurrentMember(), new Dictionary<int, EventInfo?>(), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't check cart item {CartKey} with CCOS.Api.", item.Key);
            TempData[CartMessages.ErrorKey] = CartMessages.TryAgainLater;
            return RedirectToPage("/Cart");
        }

        if (found.Problem is not null)
        {
            cart.Remove(item.Key);
            TempData[CartMessages.ErrorKey] = found.Problem;
        }
        else
        {
            cart.SetQuantity(item.Key, quantity, found.Available);
            TempData[CartMessages.MessageKey] = quantity > found.Available ? found.LoweredNotice() : "Cart updated.";
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
}
