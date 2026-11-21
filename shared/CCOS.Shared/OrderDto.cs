namespace CCOS.Shared;

/// <summary>
/// An order as sent back by POST /api/orders (CAR-91). New orders are always Pending: they hold the
/// stock and seats, and get paid in person, then an officer marks them paid.
/// </summary>
/// <param name="OrderId">The order number to show the customer.</param>
/// <param name="OrderDate">When it was placed (UTC).</param>
/// <param name="Status">Pending, Fulfilled or Canceled. Always Pending right after placing it.</param>
/// <param name="TotalAmount">What they owe for the whole order.</param>
/// <param name="Lines">What was ordered, with the price each line was charged.</param>
public record OrderDto(
    int OrderId,
    DateTime OrderDate,
    string Status,
    decimal TotalAmount,
    IReadOnlyList<OrderLineDto> Lines);

/// <summary>
/// One line on an order, with the name and price copied at checkout so later product edits
/// don't change it.
/// </summary>
/// <param name="ProductId">The merch product id or ticket type id.</param>
/// <param name="Name">The product or ticket name at the time of the order.</param>
/// <param name="EventId">The event for ticket lines. Null for merch.</param>
/// <param name="Quantity">How many.</param>
/// <param name="UnitPrice">The price charged for one (member price for current members).</param>
/// <param name="LineTotal">UnitPrice times Quantity.</param>
public record OrderLineDto(
    int ProductId,
    string Name,
    int? EventId,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);
