namespace CCOS.Shared;

/// <summary>
/// A venue as returned by GET /api/venues: just what the event form's venue dropdown needs.
/// </summary>
public record VenueDto(
    int VenueId,
    string Name);
