namespace CCOS.Data.Entities;

public class TicketRegistration
{
    public int TicketRegistrationId { get; set; }
    public int EventId { get; set; }
    public int TicketTypeId { get; set; }
    public int OrderLineId { get; set; }
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Purchased;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CanceledAtUtc { get; set; }
    public Event Event { get; set; } = null!;
    public OrderLine OrderLine { get; set; } = null!;
}
