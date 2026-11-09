namespace CCOS.Data.Entities;

/// <summary>
/// The street address of a venue. Each venue has at most one.
/// </summary>
public class VenueAddress
{
    public int VenueAddressId { get; set; }
    public int VenueId { get; set; }
    public string AddressLine1 { get; set; } = null!;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = null!;

    /// <summary>Two-letter state code, like WI.</summary>
    public string State { get; set; } = null!;

    public string ZipCode { get; set; } = null!;
    public Venue Venue { get; set; } = null!;
}
