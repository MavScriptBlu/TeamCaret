namespace CCOS.Data.Entities;

public class Venue
{
    public int VenueId { get; set; }
    public string Name { get; set; } = null!;
    public int Capacity { get; set; }
    public bool IsActive { get; set; } = true;
    public VenueAddress? Address { get; set; }
    public ICollection<Event> Events { get; set; } = new List<Event>();
}
