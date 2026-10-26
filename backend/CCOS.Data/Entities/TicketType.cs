namespace CCOS.Data.Entities;

/// <summary>
/// A kind of ticket for one event (for example "General" or "VIP"). It is a Product, so it has
/// a name and prices, and it is stored in the same table with ProductType set to "Ticket".
/// </summary>
public class TicketType : Product
{
    /// <summary>The event this ticket is for. A ticket type always has one; null only for plain merchandise rows.</summary>
    public int? EventId { get; set; }

    public Event? Event { get; set; }
}
