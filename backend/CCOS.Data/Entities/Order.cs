namespace CCOS.Data.Entities;

/// <summary>
/// One purchase. The items bought are in OrderLines. Placed through OrderRepository (CAR-91),
/// which holds the stock and seats in the same transaction.
/// </summary>
public class Order
{
    public int OrderId { get; set; }

    /// <summary>Who placed the order (CAR-91). Matches the ERD: a customer places many orders.</summary>
    public int CustomerId { get; set; }

    public DateTime OrderDate { get; set; }

    /// <summary>One of Pending, Fulfilled or Canceled (the database rejects anything else).</summary>
    public string Status { get; set; } = "Pending";

    public decimal TotalAmount { get; set; }

    public Customer Customer { get; set; } = null!;
    public ICollection<OrderLine> OrderLines { get; set; } = new List<OrderLine>();
}
