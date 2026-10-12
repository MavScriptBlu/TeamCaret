namespace CCOS.Data.Entities;

public class Product
{
    public int ProductId { get; set; }
    public string Category { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal MemberPrice { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
}
