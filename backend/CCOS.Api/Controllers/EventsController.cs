using CCOS.Data.Entities;
using CCOS.Repositories;
using CCOS.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Api.Controllers;

/// <summary>
/// Event endpoints for the Events pages. The public list only shows upcoming events
/// (active and starting from now on, soonest first). The officer endpoints create, edit, list
/// and remove events (CAR-55, CAR-56). Events are never deleted: DELETE removes one
/// (IsActive = false) and PUT with IsActive = true restores it.
/// The officer endpoints still need [Authorize(Policy = ClubPolicies.Officer)] like
/// ProductsController (CAR-69 follow-up).
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
    /// GET /api/events/officer: the officer event list (CAR-56). Every event, removed and past
    /// ones too, newest start first.
    /// TODO: restrict to officers once login exists (CAR-1: Officer only).
    /// </summary>
    [HttpGet("officer")]
    public async Task<IReadOnlyList<EventDto>> ListForOfficers(CancellationToken cancellationToken)
    {
        var items = await events.ListAllAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    /// <summary>
    /// GET /api/events/officer/{id}: one event in any state, or 404. Removed events come back too
    /// (IsActive false) so the edit page can restore them.
    /// TODO: restrict to officers once login exists (CAR-1: Officer only).
    /// </summary>
    [HttpGet("officer/{id:int}")]
    public async Task<ActionResult<EventDto>> GetForOfficers(int id, CancellationToken cancellationToken)
    {
        var @event = await events.FindAsync(id, cancellationToken);
        return @event is null ? NotFound() : ToDto(@event);
    }

    /// <summary>
    /// POST /api/events: CREATE an event and its ticket type (CAR-55). Bad or missing fields come
    /// back as 400 automatically ([ApiController]); a venue that doesn't exist or was removed
    /// comes back as 400 with the message on VenueId.
    /// TODO: restrict to officers once login exists (CAR-1: Officer only).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<EventDto>> Create(
        [FromBody] CreateEventRequest input,
        CancellationToken cancellationToken)
    {
        if (!await events.VenueAvailableAsync(input.VenueId, mustBeActive: true, cancellationToken))
        {
            ModelState.AddModelError(nameof(input.VenueId), "Pick a venue that is in the venue list.");
            return ValidationProblem(ModelState);
        }

        var created = await events.CreateAsync(
            input.Name,
            input.Description,
            input.StartsAtUtc,
            input.EndsAtUtc,
            input.VenueId,
            input.TicketCapacity,
            input.MembersOnly,
            input.TicketPrice,
            input.MemberTicketPrice,
            cancellationToken);

        var saved = await events.FindAsync(created.EventId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.EventId }, ToDto(saved!));
    }

    /// <summary>
    /// PUT /api/events/{id}: UPDATE an event and its ticket prices (CAR-55). The same checks as
    /// POST. A removed venue is fine when the event already uses it. IsActive true restores a
    /// removed event. 404 when there's no event with that id, 400 when the capacity is lower than
    /// the tickets already sold, 409 when someone else saved the event first.
    /// TODO: restrict to officers once login exists (CAR-1: Officer only).
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<EventDto>> Update(
        int id,
        [FromBody] UpdateEventRequest input,
        CancellationToken cancellationToken)
    {
        var current = await events.FindAsync(id, cancellationToken);
        if (current is null)
        {
            return NotFound();
        }

        // Keeping the venue the event already has is always fine, even if that venue was removed since.
        var venueChanged = current.VenueId != input.VenueId;
        if (!await events.VenueAvailableAsync(input.VenueId, mustBeActive: venueChanged, cancellationToken))
        {
            ModelState.AddModelError(nameof(input.VenueId), "Pick a venue that is in the venue list.");
            return ValidationProblem(ModelState);
        }

        EventUpdateResult result;
        try
        {
            result = await events.UpdateAsync(
                id,
                input.Name,
                input.Description,
                input.StartsAtUtc,
                input.EndsAtUtc,
                input.VenueId,
                input.TicketCapacity,
                input.MembersOnly,
                input.TicketPrice,
                input.MemberTicketPrice,
                input.IsActive,
                cancellationToken);
        }
        // Two officers saved the same event at once. The second one has to reload and try again.
        catch (DbUpdateConcurrencyException)
        {
            return Problem(
                title: "The event was changed by someone else.",
                detail: "Reload the event and try again.",
                statusCode: StatusCodes.Status409Conflict);
        }

        switch (result.Status)
        {
            case EventUpdateStatus.NotFound:
                return NotFound();

            case EventUpdateStatus.CapacityBelowSold:
                ModelState.AddModelError(
                    nameof(input.TicketCapacity),
                    $"{result.TicketsSold} tickets are already sold, so the capacity can't be lower than that.");
                return ValidationProblem(ModelState);

            // Saved: nothing to report, carry on and load the event to send back.
            case EventUpdateStatus.Saved:
                break;

            // A new status got added to the enum but not handled here. Fail loudly, don't pretend it saved.
            default:
                throw new InvalidOperationException($"Unhandled event update status: {result.Status}.");
        }

        var saved = await events.FindAsync(id, cancellationToken);
        return ToDto(saved!);
    }

    /// <summary>
    /// DELETE /api/events/{id}: REMOVE an event (CAR-56). It's a soft delete, so IsActive becomes
    /// false and the row stays in the database. PUT with IsActive true brings it back.
    /// 204 on success, 404 when there's no event with that id.
    /// TODO: restrict to officers once login exists (CAR-1: Officer only).
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var success = await events.SoftDeleteAsync(id, cancellationToken);
        return success ? NoContent() : NotFound();
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
