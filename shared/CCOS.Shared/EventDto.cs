namespace CCOS.Shared;

/// <summary>
/// An event as returned by GET /api/events. Includes venue name and ticket types so
/// the event pages can show everything without extra calls.
/// </summary>
public record EventDto(
    int EventId,
    string Name,
    string? Description,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    int TicketCapacity,
    bool MembersOnly,
    bool IsActive,
    int VenueId,
    string VenueName,
    IReadOnlyList<TicketTypeDto> TicketTypes);

/// <summary>
/// A ticket type for an event, for example "General" at $5 with a $0 member price.
/// </summary>
public record TicketTypeDto(
    int TicketTypeId,
    string Name,
    decimal Price,
    decimal MemberPrice,
    bool IsActive);
