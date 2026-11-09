namespace CCOS.Shared;

/// <summary>
/// An upcoming event booked at a venue, so the officer venue page can warn before the venue
/// is removed (CAR-79). Upcoming means active and not started yet.
/// </summary>
public record VenueEventSummaryDto(int EventId, string Name, DateTime StartsAtUtc);
