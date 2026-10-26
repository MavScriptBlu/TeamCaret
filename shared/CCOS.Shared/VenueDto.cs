namespace CCOS.Shared;

/// <summary>
/// A venue as returned by GET /api/venues. Used for the event form's venue dropdown.
/// </summary>
public record VenueDto(int VenueId, string Name);
