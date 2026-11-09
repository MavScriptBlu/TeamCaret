namespace CCOS.Data.Entities;

/// <summary>
/// Something the club sells: swag, resources, and so on. Tickets are also products
/// (see TicketType) and share the same table.
/// </summary>
public class Product
{
    public int ProductId { get; set; }
    public string Category { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>The regular price.</summary>
    public decimal Price { get; set; }

    /// <summary>The price for club members.</summary>
    public decimal MemberPrice { get; set; }

    public int StockQuantity { get; set; }

    /// <summary>
    /// False means the product was deactivated (soft deleted). Normal queries skip it;
    /// only officer screens use IgnoreQueryFilters() to see it.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
