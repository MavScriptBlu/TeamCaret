using System.ComponentModel.DataAnnotations;

namespace CCOS.Shared;

/// <summary>
/// Request body for POST /api/orders (CAR-91): everything in the cart, merch and tickets together.
/// Only ids and quantities come in. Prices, names and stock are always looked up on the server,
/// so nobody can pick their own price by editing the request.
/// </summary>
/// <param name="Lines">What to buy. At least one line.</param>
public record PlaceOrderRequest(
    [Required(ErrorMessage = "Add at least one item to the order.")]
    [MinLength(1, ErrorMessage = "Add at least one item to the order.")]
    [MaxLength(PlaceOrderRequest.MaxLines, ErrorMessage = "An order can have at most 50 lines.")]
    IReadOnlyList<OrderLineRequest> Lines)
{
    /// <summary>The most lines one order can have. A real cart never gets close; this just stops junk requests.</summary>
    public const int MaxLines = 50;
}

/// <summary>
/// One line of an order: a merch product or a ticket type, and how many. Ticket types are products
/// too, so both use ProductId, the same id the cart keeps (CAR-92).
/// </summary>
/// <param name="ProductId">The merch product id, or the ticket type id for tickets.</param>
/// <param name="Quantity">How many. 1 to 100.</param>
public record OrderLineRequest(
    [Range(1, int.MaxValue, ErrorMessage = "Pick a product or ticket for every line.")]
    int ProductId,

    [Range(1, OrderLineRequest.MaxQuantity, ErrorMessage = "Quantity must be from 1 to 100.")]
    int Quantity)
{
    /// <summary>The most of one thing a single line can ask for.</summary>
    public const int MaxQuantity = 100;
}
