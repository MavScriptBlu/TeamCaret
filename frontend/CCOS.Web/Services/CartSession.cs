using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;

namespace CCOS.Web.Services;

/// <summary>
/// Keeps the cart in the visitor's website session (CAR-92), so nothing is saved in the database.
/// The session lives in the website's memory and is tied to a cookie, so the cart lasts until the
/// visitor has been away for a couple of hours or the website restarts.
/// </summary>
public static class CartSession
{
    /// <summary>The session key the cart is saved under.</summary>
    public const string Key = "ccos.cart";

    /// <summary>
    /// The visitor's cart. Empty when there isn't one yet, or when there's no session at all
    /// (some tests), so pages and the menu can always call this.
    /// </summary>
    public static Cart GetCart(this HttpContext? context)
    {
        var session = context?.Features.Get<ISessionFeature>()?.Session;
        var json = session?.GetString(Key);
        if (string.IsNullOrEmpty(json))
        {
            return new Cart();
        }

        try
        {
            return new Cart(JsonSerializer.Deserialize<List<CartItem>>(json) ?? []);
        }
        catch (JsonException)
        {
            // An old or broken cart: start over instead of crashing the page.
            return new Cart();
        }
    }

    /// <summary>
    /// Empties the cart. Log out calls this, so the next person on a shared computer doesn't see
    /// the last person's cart. Does nothing when there's no session.
    /// </summary>
    public static void ClearCart(this HttpContext context)
    {
        context.Features.Get<ISessionFeature>()?.Session?.Remove(Key);
    }

    /// <summary>Saves the cart in the visitor's session.</summary>
    public static void SaveCart(this HttpContext context, Cart cart)
    {
        context.Session.SetString(Key, JsonSerializer.Serialize(cart.Items));
    }
}
