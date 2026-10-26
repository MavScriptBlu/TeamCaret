using CCOS.Data.Entities;
using CCOS.Repositories;
using CCOS.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Api.Controllers;

/// <summary>
/// Event endpoints for the Events pages. The public list only shows upcoming events
/// (active and starting from now on, soonest first).
/// </summary>
[ApiController]
[Route("api/events")]
public class EventsController(EventRepository events) : ControllerBase
{
    /// <summary>
    /// GET /api/events: upcoming events with venue name and ticket types.
    /// The "Upcoming events" query: IsActive and StartsAtUtc from now on, soonest first.
    /// </summary>
    [HttpGet]
    public async Task<IReadOnlyList<EventDto>> List(CancellationToken cancellationToken)
    {
        var items = await events.ListUpcomingAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    /// <summary>
    /// GET /api/events/{id}: one event with venue name and active ticket types.
    /// Returns 404 for unknown or inactive events.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<EventDto>> Get(int id, CancellationToken cancellationToken)
    {
        var @event = await events.FindActiveAsync(id, cancellationToken);

        if (@event is null)
        {
            return NotFound();
        }

        return ToDto(@event);
    }

    /// <summary>
    /// Turns an Event entity into the DTO shape sent over the wire.
    /// </summary>
    private static EventDto ToDto(Event @event) => new(
        @event.EventId,
        @event.Name,
        @event.Description,
        @event.StartsAtUtc,
        @event.EndsAtUtc,
        @event.TicketCapacity,
        @event.MembersOnly,
        @event.IsActive,
        @event.VenueId,
        @event.Venue.Name,
        @event.TicketTypes
            .Where(t => t.IsActive)
            .Select(t => new TicketTypeDto(t.ProductId, t.Name, t.Price, t.MemberPrice, t.IsActive))
            .ToList());
}
