namespace CCOS.Data.Entities;

/// <summary>
/// One ticket someone bought for an event. Canceling keeps the row and changes Status,
/// so there is always a record of what happened.
/// </summary>
public class TicketRegistration
{
    public int TicketRegistrationId { get; set; }
    public int EventId { get; set; }
    public int TicketTypeId { get; set; }

    /// <summary>The order line this ticket was bought on.</summary>
    public int OrderLineId { get; set; }

    public RegistrationStatus Status { get; set; } = RegistrationStatus.Purchased;
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>When the ticket was canceled. Null while the ticket is still Purchased.</summary>
    public DateTime? CanceledAtUtc { get; set; }

    public Event Event { get; set; } = null!;
    public OrderLine OrderLine { get; set; } = null!;
}
