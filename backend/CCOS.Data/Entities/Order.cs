namespace CCOS.Data.Entities;

/// <summary>
/// One purchase. The items bought are in OrderLines.
/// </summary>
public class Order
{
    public int OrderId { get; set; }

    //  customer who placed the order. Nullable because we allow orders to be placed without a customer account.
    public int? CustomerId { get; set; }

    public DateTime OrderDate { get; set; }

    // One of Pending, Paid, Picked up or Canceled (the database rejects anything else)
    public string Status { get; set; } = "Pending";

    public decimal TotalAmount { get; set; }
    public uint RowVersion { get; set; }

    public Customer? Customer { get; set; }
    public ICollection<OrderLine> OrderLines { get; set; } = new List<OrderLine>();

    public bool TryTransitionTo(string nextStatus)
    {
        var allowed = Status switch
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

public static class OrderStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string PickedUp = "Picked up";
    public const string Canceled = "Canceled";
}
