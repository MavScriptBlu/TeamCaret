using CCOS.Data.Entities;

namespace CCOS.Repositories;

/// <summary>
/// How placing an order went (CAR-91). The controller turns each one into a status code.
/// </summary>
public enum PlaceOrderOutcome
{
    /// <summary>Saved. The stock and seats are held and the order is Pending.</summary>
    Placed,

    /// <summary>Something can't be sold right now (gone, out of stock, sold out, already started). Nothing was saved.</summary>
    ItemUnavailable,

    /// <summary>A ticket is for a members-only event and the customer isn't a current member. Nothing was saved.</summary>
    MembersOnly,
}

/// <summary>
/// What OrderRepository.PlaceOrderAsync hands back: the saved order, or which item stopped it and why.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Order">The saved order, with its lines. Only set when Outcome is Placed.</param>
/// <param name="ProblemProductId">The product or ticket type that stopped the order. Null when it was placed.</param>
/// <param name="Message">A plain sentence that names the item, ready to show the customer. Null when it was placed.</param>
public sealed record PlaceOrderResult(
    PlaceOrderOutcome Outcome,
    Order? Order,
    int? ProblemProductId,
    string? Message)
{
    /// <summary>The order went through.</summary>
    public static PlaceOrderResult Placed(Order order) => new(PlaceOrderOutcome.Placed, order, null, null);

    /// <summary>One item can't be sold right now.</summary>
    public static PlaceOrderResult Unavailable(int productId, string message) =>
        new(PlaceOrderOutcome.ItemUnavailable, null, productId, message);

    /// <summary>A members-only ticket for someone who isn't a current member.</summary>
    public static PlaceOrderResult MembersOnlyRefused(int productId, string message) =>
        new(PlaceOrderOutcome.MembersOnly, null, productId, message);
}
