using System.Net;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages;

/// <summary>
/// Checkout (/Checkout, CAR-93): one last look at the cart, then "Place order" sends it to
/// POST /api/orders (CAR-91). Reserve now, pay in person: no online payment (CAR-87).
/// Signing in is required, because the order belongs to a customer; a visitor who isn't signed in
/// goes to Log in and comes back here, with the same cart.
/// </summary>
[Authorize]
public class CheckoutModel(
    ProductsApiClient productsApi,
    EventsApiClient eventsApi,
    OrdersApiClient ordersApi,
    ILogger<CheckoutModel> logger) : PageModel
{
    /// <summary>TempData key that carries the placed order to the confirmation page.</summary>
    public const string PlacedOrderKey = "PlacedOrder";

    /// <summary>Shown when the API can't be reached or answers with an error we didn't expect.</summary>
    public const string CouldNotPlace = "We couldn't place your order right now. Your cart is still saved, so please try again in a few minutes.";

    /// <summary>Shown when the cart changed while it was checked again, so the order wasn't sent.</summary>
    public const string CartChanged = "Your cart changed since you last looked. Check it below, then place your order again.";

    private readonly CartLoader _loader = new(productsApi, eventsApi);

    /// <summary>The cart's lines, ready to show.</summary>
    public IReadOnlyList<CartLine> Lines { get; private set; } = [];

    /// <summary>What was taken out or lowered while the cart was checked again.</summary>
    public IReadOnlyList<string> Notices { get; private set; } = [];

    /// <summary>The order total.</summary>
    public decimal Total => Lines.Sum(line => line.LineTotal);

    /// <summary>The order total as text, for example "$45.00".</summary>
    public string TotalText => Money.Format(Total);

    /// <summary>Why the order wasn't placed, or null.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>The key of the cart line that stopped the order (sold out, members only), so the page can mark it.</summary>
    public string? ProblemKey { get; private set; }

    /// <summary>True when the cart couldn't be loaded, so there's nothing to show or place.</summary>
    public bool LoadFailed { get; private set; }

    /// <summary>
    /// Shows the cart one more time. An empty cart goes back to the Cart page.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var cart = HttpContext.GetCart();
        if (cart.IsEmpty)
        {
            return RedirectToPage("/Cart");
        }

        var loaded = await LoadAsync(cart, cancellationToken);
        if (loaded is null)
        {
            return Page();
        }

        return Lines.Count == 0 ? RedirectToPage("/Cart") : Page();
    }

    /// <summary>
    /// The Place order button. Checks the cart again first; if anything changed (sold out, lowered),
    /// nothing is sent and the visitor looks again. Otherwise the order goes to the API. Placed:
    /// the cart is emptied and the confirmation page shows the order. Refused: the cart stays and
    /// the message says which item to change.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var cart = HttpContext.GetCart();
        if (cart.IsEmpty)
        {
            return RedirectToPage("/Cart");
        }

        var loaded = await LoadAsync(cart, cancellationToken);
        if (loaded is null)
        {
            ErrorMessage = CouldNotPlace;
            return Page();
        }

        if (Lines.Count == 0)
        {
            return RedirectToPage("/Cart");
        }

        if (loaded.Changed)
        {
            ErrorMessage = CartChanged;
            return Page();
        }

        var request = new PlaceOrderRequest(cart.Items
            .Select(item => new OrderLineRequest(item.ProductId, item.Quantity))
            .ToList());

        PlaceOrderResponse response;
        try
        {
            response = await ordersApi.PlaceAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't place an order with CCOS.Api.");
            ErrorMessage = CouldNotPlace;
            return Page();
        }

        if (response.IsPlaced)
        {
            HttpContext.ClearCart();
            TempData[PlacedOrderKey] = JsonSerializer.Serialize(response.Order);
            return RedirectToPage("/Checkout/Confirmation");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // The sign-in ran out while the page was open. Log in again and come back; the cart stays.
            return RedirectToPage("/Account/Login", new { returnUrl = "/Checkout", loginFirst = true });
        }

        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.Forbidden)
        {
            var problemItem = cart.Items.FirstOrDefault(item => item.ProductId == response.ProblemProductId);
            var problemLine = Lines.FirstOrDefault(line => line.Key == problemItem?.Key);
            ProblemKey = problemLine?.Key;
            ErrorMessage = ProblemMessage(response.Message, problemLine?.Name);
            return Page();
        }

        logger.LogError("CCOS.Api answered {StatusCode} when placing an order.", (int)response.StatusCode);
        ErrorMessage = CouldNotPlace;
        return Page();
    }

    /// <summary>
    /// The sentence shown when the API refused the order. The API's own sentence names the item,
    /// except for an item that's no longer sold, which it can only name by its id; then the cart
    /// line's name is used instead.
    /// </summary>
    /// <param name="apiMessage">The API's "detail" sentence, or null.</param>
    /// <param name="lineName">The name of the cart line that stopped the order, or null.</param>
    public static string ProblemMessage(string? apiMessage, string? lineName)
    {
        var namesOnlyAnId = apiMessage is null || apiMessage.StartsWith("Item ", StringComparison.Ordinal);
        if (!namesOnlyAnId)
        {
            return $"{apiMessage} Change it in your cart, then try again.";
        }

        return lineName is null
            ? "Something in your cart isn't available anymore. Check your cart, then try again."
            : $"{lineName} isn't available anymore. Change it in your cart, then try again.";
    }

    // Loads and checks the cart, saving it when something was taken out or lowered. Null when the
    // API couldn't be reached (LoadFailed is then set).
    private async Task<CartLoadResult?> LoadAsync(Cart cart, CancellationToken cancellationToken)
    {
        CartLoadResult loaded;
        try
        {
            loaded = await _loader.LoadAsync(cart, User.IsCurrentMember(), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't load the cart's items from CCOS.Api for checkout.");
            LoadFailed = true;
            return null;
        }

        if (loaded.Changed)
        {
            HttpContext.SaveCart(cart);
        }

        Lines = loaded.Lines;
        Notices = loaded.Notices;
        return loaded;
    }
}
