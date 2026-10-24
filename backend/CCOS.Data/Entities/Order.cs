namespace CCOS.Data.Entities;

/// <summary>
/// One purchase. The items bought are in OrderLines.
/// </summary>
public class Order
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }

    /// <summary>One of Pending, Fulfilled or Canceled (the database rejects anything else).</summary>
    public string Status { get; set; } = "Pending";

    public decimal TotalAmount { get; set; }
    public ICollection<OrderLine> OrderLines { get; set; } = new List<OrderLine>();
}
