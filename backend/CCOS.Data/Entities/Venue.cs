namespace CCOS.Data.Entities;

/// <summary>
/// A place where events are held.
/// </summary>
public class Venue
{
    public int VenueId { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>How many people the room holds.</summary>
    public int Capacity { get; set; }

    /// <summary>False means the venue was deactivated (soft deleted).</summary>
    public bool IsActive { get; set; } = true;

    public VenueAddress? Address { get; set; }
    public ICollection<Event> Events { get; set; } = new List<Event>();
}
