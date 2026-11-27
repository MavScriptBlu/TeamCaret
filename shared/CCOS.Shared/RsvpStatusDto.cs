namespace CCOS.Shared;

/// <summary>
/// Whether the signed-in person is going to an event (CAR-94), from GET /api/events/{id}/rsvp.
/// </summary>
/// <param name="Going">True when they hold at least one ticket for the event.</param>
/// <param name="Tickets">How many tickets they hold for the event.</param>
/// <param name="CanCancel">True when at least one is a free RSVP, which Cancel RSVP can give back.</param>
public record RsvpStatusDto(bool Going, int Tickets, bool CanCancel);
