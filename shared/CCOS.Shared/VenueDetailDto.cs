namespace CCOS.Shared;

/// <summary>
/// One venue with its address, as returned by GET /api/venues/{id} and the officer venue
/// list (CAR-57). The address parts are null for a venue that was saved without one
/// (the sample venues, for example). IsActive false means the venue was removed.
/// </summary>
public record VenueDetailDto(
    int VenueId,
    string Name,
    int Capacity,
    bool IsActive,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? ZipCode);
