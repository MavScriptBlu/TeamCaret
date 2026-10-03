namespace CCOS.Data.Entities;

public class TicketType : Product
{
    public int? EventId { get; set; }
    public Event? Event { get; set; }
}
