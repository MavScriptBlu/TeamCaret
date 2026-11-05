using CCOS.Data.Entities;
using CCOS.Repositories;
using CCOS.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    /// POST /api/events: creates an event and its first ticket type.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<EventDto>> Create([FromBody] CreateEventRequest input, CancellationToken cancellationToken)
    {
        // TODO(CAR-69): Add [Authorize(Policy = "Officer")] when the Officer policy lands.
        var result = await events.CreateAsync(
            input.VenueId,
            input.Name,
            input.Description,
            input.StartsAtUtc,
            input.EndsAtUtc,
            input.TicketCapacity,
            input.MembersOnly,
            input.Price,
            input.MemberPrice,
            cancellationToken);

        if (result.Errors.Count > 0)
        {
            return ToValidationProblem(result.Errors);
        }

        var createdEvent = result.Event!;

        return CreatedAtAction(nameof(Get), new { id = createdEvent.EventId }, ToDto(createdEvent));
    }

    /// <summary>
    /// PUT /api/events/{id}: updates the fields allowed by UpdateEventRequest
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<EventDto>> Update(int id, [FromBody] UpdateEventRequest input, CancellationToken cancellationToken)
    {
        EventWriteResult? result;

        try
        {
            result = await events.UpdateAsync(
                id,
                input.VenueId,
                input.Name,
                input.Description,
                input.StartsAtUtc,
                input.EndsAtUtc,
                input.TicketCapacity,
                input.MembersOnly,
                input.RowVersion,
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "This event was changed by someone else",
                Detail = "Reload the event and reapply your changes"
            });
        }

        if (result is null)
        {
            return NotFound();
        }

        if (result.Errors.Count > 0)
        {
            return ToValidationProblem(result.Errors);
        }

        return ToDto(result.Event!);
    }

    /// <summary>
    /// DELETE /api/events/{id}: deactivates the event without removing its record
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        // TODO(CAR-69): Add [Authorize(Policy = "Officer")] when the Officer policy lands.
        try
        {
            var deactivated = await events.DeactivateAsync(id, cancellationToken);
            return deactivated ? NoContent() : NotFound();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "This event was changed by someone else.",
                Detail = "Reload the event and try again."
            });
        }
    }

    // takes a dictionary of field names to error messages and adds them to ModelState, then returns a ValidationProblem result
    private ActionResult ToValidationProblem(IReadOnlyDictionary<string, string[]> errors)
    {
        foreach (var (field, messages) in errors)
        {
            foreach (var message in messages)
            {
                ModelState.AddModelError(field, message);
            }
        }

        return ValidationProblem(ModelState);
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
            .ToList(),
        @event.RowVersion);
}
