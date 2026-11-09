namespace CCOS.Data.Entities;

/// <summary>
/// A club event (meetup, workshop, and so on) that members can buy tickets for.
/// </summary>
public class Event
{
    public int EventId { get; set; }
    public int VenueId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    // Times are always stored in UTC and converted to local time only when shown on screen.
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }

    /// <summary>The most tickets that can be sold for this event.</summary>
    public int TicketCapacity { get; set; }

    /// <summary>
    /// How many tickets are currently sold. Only TicketBookingRepository changes this,
    /// so overselling is blocked. The database also refuses a value above TicketCapacity.
    /// </summary>
    public int TicketsSold { get; set; }

    /// <summary>True when only club members may buy tickets.</summary>
    public bool MembersOnly { get; set; }

    /// <summary>False means the event was deactivated (soft deleted) and no longer shows up.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Changes every time the row is saved. EF uses it to catch two people editing the same
    /// event at once. See the Event setup in AppDbContext.
    /// </summary>
    public uint RowVersion { get; set; }

    public Venue Venue { get; set; } = null!;
    public ICollection<TicketType> TicketTypes { get; set; } = new List<TicketType>();
    public ICollection<TicketRegistration> Registrations { get; set; } = new List<TicketRegistration>();
}
