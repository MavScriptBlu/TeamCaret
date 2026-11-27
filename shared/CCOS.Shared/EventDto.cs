namespace CCOS.Shared;

/// <summary>
/// An event as returned by GET /api/events. Includes venue name and ticket types so
/// the event pages can show everything without extra calls. TicketsSold (CAR-94) lets the event
/// page say "Sold out"; it's last and optional so older callers keep working.
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
    IReadOnlyList<TicketTypeDto> TicketTypes,
    uint RowVersion,
    int TicketsSold = 0);

/// <summary>
/// A ticket type for an event, for example "General" at $5 with a $0 member price.
/// MemberPrice is null on the public event endpoints unless the caller is a current member (CAR-69);
/// the officer endpoints always include it.
/// </summary>
public record TicketTypeDto(
    int TicketTypeId,
    string Name,
    decimal Price,
    decimal? MemberPrice,
    bool IsActive);
