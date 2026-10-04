namespace CCOS.Data.Entities;

public class Order
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal TotalAmount { get; set; }
    public ICollection<OrderLine> OrderLines { get; set; } = new List<OrderLine>();
}
