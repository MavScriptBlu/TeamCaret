namespace CCOS.Data.Entities;

/// <summary>
/// One product on an order, with how many were bought. The price and name are copied from
/// the product when the order is placed, so later edits to the product don't rewrite history.
/// </summary>
public class OrderLine
{
    public int OrderLineId { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }

    /// <summary>The price per item at the time of purchase.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>The product name at the time of purchase.</summary>
    public string ProductNameSnapshot { get; set; } = null!;

    public Order Order { get; set; } = null!;
}
