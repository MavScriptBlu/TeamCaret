using CCOS.Data.Entities;
using CCOS.Repositories;
using CCOS.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Api.Controllers;

/// <summary>
/// Event endpoints for the Events pages. The public list only shows upcoming events
/// (active and starting from now on, soonest first). The officer endpoints create, edit, list
/// and remove events (CAR-55, CAR-56). Events are never deleted: DELETE removes one
/// (IsActive = false) and PUT with IsActive = true restores it.
/// The officer endpoints need the Officer policy (CAR-69): 401 when not signed in, 403 when
/// signed in but not an officer.
/// </summary>
[ApiController]
[Route("api/events")]
public class EventsController(EventRepository events, IAuthorizationService authorization) : ControllerBase
{
    /// <summary>
    /// GET /api/events: upcoming events with venue name and ticket types.
    /// The "Upcoming events" query: IsActive and StartsAtUtc from now on, soonest first.
    /// Ticket member prices only go to current members (CAR-69); everyone else gets null.
    /// </summary>
    [HttpGet]
    public async Task<IReadOnlyList<EventDto>> List(CancellationToken cancellationToken)
    {
        var items = await events.ListUpcomingAsync(cancellationToken);
        var includeMemberPrice = await CanSeeMemberPriceAsync();
        return items.Select(@event => ToDto(@event, includeMemberPrice)).ToList();
    }

    /// <summary>
    /// GET /api/events/{id}: one upcoming event with venue name and active ticket types, the same
    /// rule as the list (active, starting from now on). Unknown, removed and past events are 404;
    /// officers open those through GET /api/events/officer/{id}. Ticket member prices only go to
    /// current members (CAR-69).
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<EventDto>> Get(int id, CancellationToken cancellationToken)
    {
        var @event = await events.FindUpcomingAsync(id, cancellationToken);

        if (@event is null)
        {
            return NotFound();
        }

        return ToDto(@event, await CanSeeMemberPriceAsync());
    }

    /// <summary>
    /// GET /api/events/officer: the officer event list (CAR-56). Every event, removed and past
    /// ones too, newest start first.
    /// </summary>
    [HttpGet("officer")]
    [Authorize(Policy = ClubPolicies.Officer)]
    public async Task<IReadOnlyList<EventDto>> ListForOfficers(CancellationToken cancellationToken)
    {
        var items = await events.ListAllAsync(cancellationToken);
        return items.Select(@event => ToDto(@event, includeMemberPrice: true)).ToList();
    }

    /// <summary>
    /// GET /api/events/officer/{id}: one event in any state, or 404. Removed events come back too
    /// (IsActive false) so the edit page can restore them.
    /// </summary>
    [HttpGet("officer/{id:int}")]
    [Authorize(Policy = ClubPolicies.Officer)]
    public async Task<ActionResult<EventDto>> GetForOfficers(int id, CancellationToken cancellationToken)
    {
        var @event = await events.FindAsync(id, cancellationToken);
        return @event is null ? NotFound() : ToDto(@event, includeMemberPrice: true);
    }

    /// <summary>
    /// POST /api/events: CREATE an event and its ticket type (CAR-55). Bad or missing fields come
    /// back as 400 automatically ([ApiController]); a venue that doesn't exist or was removed
    /// comes back as 400 with the message on VenueId.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = ClubPolicies.Officer)]
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
        return CreatedAtAction(nameof(Get), new { id = created.EventId }, ToDto(saved!, includeMemberPrice: true));
    }

    /// <summary>
    /// PUT /api/events/{id}: UPDATE an event and its ticket prices (CAR-55). The same checks as
    /// POST. A removed venue is fine when the event already uses it. IsActive true restores a
    /// removed event. 404 when there's no event with that id, 400 when the capacity is lower than
    /// the tickets already sold, 409 when someone else saved the event first.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = ClubPolicies.Officer)]
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
        return ToDto(saved!, includeMemberPrice: true);
    }

    /// <summary>
    /// DELETE /api/events/{id}: REMOVE an event (CAR-56). It's a soft delete, so IsActive becomes
    /// false and the row stays in the database. PUT with IsActive true brings it back.
    /// 204 on success, 404 when there's no event with that id.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = ClubPolicies.Officer)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var success = await events.SoftDeleteAsync(id, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    /// <summary>
    /// Same rule as products (CAR-69): only a signed-in current member (dues paid through today)
    /// gets member prices, checked against the database on every request.
    /// </summary>
    private async Task<bool> CanSeeMemberPriceAsync() =>
        User.Identity?.IsAuthenticated == true
        && (await authorization.AuthorizeAsync(User, ClubPolicies.Member)).Succeeded;

    /// <summary>
    /// Turns an Event entity into the DTO shape sent over the wire. Ticket member prices are left
    /// out (null) when includeMemberPrice is false.
    /// </summary>
    private static EventDto ToDto(Event @event, bool includeMemberPrice) => new(
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
            .Select(t => new TicketTypeDto(t.ProductId, t.Name, t.Price, includeMemberPrice ? t.MemberPrice : null, t.IsActive))
            .ToList());
}
