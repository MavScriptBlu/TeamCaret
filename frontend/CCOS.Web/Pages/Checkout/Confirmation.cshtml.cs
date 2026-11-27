using System.Text.Json;
using CCOS.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Checkout;

/// <summary>
/// Order confirmation (/Checkout/Confirmation, CAR-93): the order number, items and total right
/// after Place order. The order arrives from Checkout once, through TempData, so refreshing the
/// page later shows a short "your order was placed" message instead. Listing past orders comes
/// with My orders (CAR-95).
/// </summary>
[Authorize]
public class ConfirmationModel : PageModel
{
    /// <summary>The order that was just placed, or null when the page was opened again later.</summary>
    public OrderDto? Order { get; private set; }

    /// <summary>Reads the placed order handed over by Checkout.</summary>
    public void OnGet()
    {
        if (TempData[CheckoutModel.PlacedOrderKey] is not string json)
        {
            return;
        }

        try
        {
            Order = JsonSerializer.Deserialize<OrderDto>(json);
        }
        catch (JsonException)
        {
            Order = null;
        }
    }
}
