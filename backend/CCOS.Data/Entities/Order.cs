using CCOS.Shared;

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

    /// <summary>One of Pending, Paid, Picked up or Canceled (the database rejects anything else).</summary>
    public string Status { get; set; } = "Pending";

    public decimal TotalAmount { get; set; }
    public uint RowVersion { get; set; }

    public Customer Customer { get; set; } = null!;
    public ICollection<OrderLine> OrderLines { get; set; } = new List<OrderLine>();

    public bool TryTransitionTo(string nextStatus)
    {
        bool allowed = Status switch
        {
            OrderStatuses.Pending => nextStatus is OrderStatuses.Paid or OrderStatuses.Canceled,
            OrderStatuses.Paid => nextStatus is OrderStatuses.PickedUp or OrderStatuses.Canceled,
            _ => false,
        };

        if (!allowed)
        {
            return false;
        }

        Status = nextStatus;
        return true;
    }
}
