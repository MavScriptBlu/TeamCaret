using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace CCOS.Web.Services;

/// <summary>
/// The message an Add to cart button shows (CAR-92), drawn by Pages/Shared/_CartMessage.cshtml.
/// </summary>
public static class CartMessages
{
    /// <summary>TempData key for "Added to your cart" messages.</summary>
    public const string MessageKey = "CartMessage";

    /// <summary>TempData key for "couldn't add" messages.</summary>
    public const string ErrorKey = "CartError";

    /// <summary>Shown when the quantity box is empty, 0 or negative.</summary>
    public const string PickAQuantity = "Choose a quantity of 1 or more.";

    /// <summary>Shown when the API can't be reached, so stock or seats can't be checked.</summary>
    public const string TryAgainLater = "Your cart couldn't be updated right now. Please try again later.";

    /// <summary>
    /// Saves the right message for what happened, for the page to show after it reloads.
    /// </summary>
    /// <param name="tempData">The page's TempData.</param>
    /// <param name="result">What <see cref="Cart.Add"/> returned.</param>
    /// <param name="name">What was added, for example "Club T-Shirt".</param>
    /// <param name="quantity">How many the visitor asked for.</param>
    /// <param name="inCart">How many are in the cart now.</param>
    /// <param name="available">How many there are in all (stock or seats left).</param>
    public static void Put(ITempDataDictionary tempData, CartAddResult result, string name, int quantity, int inCart, int available)
    {
        switch (result)
        {
            case CartAddResult.Added:
                tempData[MessageKey] = $"Added {quantity} × {name} to your cart.";
                break;
            case CartAddResult.LimitedToAvailable:
                tempData[MessageKey] = $"Only {available} available, so your cart now has {inCart} × {name}.";
                break;
            default:
                tempData[ErrorKey] = available <= 0
                    ? $"{name} is sold out."
                    : $"Your cart already has all {available} available.";
                break;
        }
    }
}
