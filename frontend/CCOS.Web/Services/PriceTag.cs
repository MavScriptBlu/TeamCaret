namespace CCOS.Web.Services;

/// <summary>
/// The prices one product or event shows, drawn by the shared _PriceTag partial (CAR-78).
/// When there's a member price, it's the big one and the regular price is shown smaller and crossed out.
/// </summary>
/// <param name="Regular">Regular price text, for example "$35.00" or "from $5.00". Null when it doesn't apply.</param>
/// <param name="Member">
/// Member price text. Null when the visitor isn't a current member (the API leaves it out, CAR-69)
/// or when it's the same as the regular price.
/// </param>
/// <param name="Large">True on a details page, false on a product card.</param>
/// <param name="RegularLabel">Optional label before a regular price shown on its own, for example "Ticket price".</param>
public sealed record PriceTag(string? Regular, string? Member, bool Large = false, string? RegularLabel = null)
{
    /// <summary>
    /// True when there's nothing to show (no regular price and no member price).
    /// </summary>
    public bool IsEmpty => Regular is null && Member is null;

    /// <summary>
    /// Prices for a product. The member price only shows when it's lower than the regular price,
    /// so a member price equal to the regular price doesn't show a crossed-out "deal".
    /// </summary>
    /// <param name="price">The regular price.</param>
    /// <param name="memberPrice">The member price, or null when the visitor can't see it.</param>
    /// <param name="large">True on the product page, false on a product card.</param>
    public static PriceTag ForProduct(decimal price, decimal? memberPrice, bool large = false) =>
        new(
            Money.Format(price),
            memberPrice is decimal member && member < price ? Money.Format(member) : null,
            large);
}
