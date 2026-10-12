namespace CCOS.Data.Entities;

public class Event
{
    public int EventId { get; set; }
    public int VenueId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public int TicketCapacity { get; set; }
    public int TicketsSold { get; set; }
    public bool MembersOnly { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
    public Venue Venue { get; set; } = null!;
    public ICollection<TicketType> TicketTypes { get; set; } = new List<TicketType>();
    public ICollection<TicketRegistration> Registrations { get; set; } = new List<TicketRegistration>();
}
