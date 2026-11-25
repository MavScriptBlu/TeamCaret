namespace CCOS.Shared;

/// <summary>
/// An order in the officer order list. Names and prices in Items are the purchase-time snapshots.
/// </summary>
public record OrderDto(
    int OrderId,
    string? CustomerName,
    DateTime OrderDateUtc,
    IReadOnlyList<OrderLineDto> Items,
    decimal TotalAmount,
    string Status,
    uint RowVersion);

/// <summary>A purchased item with the name and price recorded when it was ordered.</summary>
public record OrderLineDto(
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

/// <summary>The destination status and concurrency token for an officer status change.</summary>
public record ChangeOrderStatusRequest(string? Status, uint? RowVersion);