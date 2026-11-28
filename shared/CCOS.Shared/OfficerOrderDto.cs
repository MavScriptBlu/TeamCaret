namespace CCOS.Shared;

/// <summary>An order and its purchase-time details for the Officer order list.</summary>
public record OfficerOrderDto(
    int OrderId,
    string? CustomerName,
    DateTime OrderDateUtc,
    IReadOnlyList<OfficerOrderLineDto> Items,
    decimal TotalAmount,
    string Status,
    uint RowVersion);

/// <summary>A purchased item using the name and price saved when the order was placed.</summary>
public record OfficerOrderLineDto(
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

/// <summary>The destination status and concurrency token for an Officer status update.</summary>
public record ChangeOrderStatusRequest(string? Status, uint? RowVersion);