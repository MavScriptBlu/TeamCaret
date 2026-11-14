using CCOS.Data.Entities;
using CCOS.Repositories;
using CCOS.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Api.Controllers;

/// <summary>
/// Venue endpoints. The website's venue form saves through POST, the event form's venue
/// dropdown reads from GET, and the officer venue pages list, open, edit and remove venues (CAR-57).
/// Venues are never deleted: DELETE removes one (IsActive = false) and PUT with IsActive = true
/// restores it.
/// </summary>
[ApiController]
[Route("api/venues")]
public class VenuesController(VenueRepository venues) : ControllerBase
{
    /// <summary>
    /// GET /api/venues: active venues, by name.
    /// </summary>
    [HttpGet]
    public async Task<IReadOnlyList<VenueDto>> List(CancellationToken cancellationToken)
    {
        var items = await venues.ListActiveAsync(cancellationToken);
        return items.Select(venue => new VenueDto(venue.VenueId, venue.Name)).ToList();
    }

    /// <summary>
    /// GET /api/venues/officer: the officer venue list (CAR-57). Every venue, removed ones too,
    /// with its address and its upcoming events (CAR-79), by name.
    /// </summary>
    [HttpGet("officer")]
    [Authorize(Policy = ClubPolicies.Officer)]
    public async Task<IReadOnlyList<VenueDetailDto>> ListForOfficers(CancellationToken cancellationToken)
    {
        var items = await venues.ListAllAsync(cancellationToken);
        var upcoming = await venues.ListUpcomingEventsByVenueAsync(cancellationToken: cancellationToken);
        return items.Select(venue => ToDetailDto(venue, upcoming[venue.VenueId])).ToList();
    }

    /// <summary>
    /// GET /api/venues/{id}: one venue with its address and upcoming events, or 404. Removed
    /// venues come back too (IsActive false) so the edit page can restore them.
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = ClubPolicies.Officer)] // only the officer pages use it, and it includes removed venues (CAR-69)
    public async Task<ActionResult<VenueDetailDto>> Get(int id, CancellationToken cancellationToken)
    {
        var venue = await venues.FindAsync(id, cancellationToken);

        if (venue is null)
        {
            return NotFound();
        }

        return await ToDetailDtoWithEventsAsync(venue, cancellationToken);
    }

    /// <summary>
    /// POST /api/venues: CREATE a venue and its address (CAR-51). Missing or too-long fields
    /// and a capacity under 1 come back as 400 automatically ([ApiController]); a name that's
    /// already used comes back as 400 with the message on Name. Officers only (CAR-69):
    /// 401 when not signed in, 403 when signed in but not an officer.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = ClubPolicies.Officer)]
    public async Task<ActionResult<VenueDto>> Create(
        [FromBody] CreateVenueRequest input,
        CancellationToken cancellationToken)
    {
        if (await venues.NameExistsAsync(input.Name, cancellationToken: cancellationToken))
        {
            ModelState.AddModelError(nameof(input.Name), "A venue with this name already exists.");
            return ValidationProblem(ModelState);
        }

        var venue = await venues.CreateAsync(
            input.Name,
            input.AddressLine1,
            input.AddressLine2,
            input.City,
            input.State,
            input.ZipCode,
            input.Capacity,
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new VenueDto(venue.VenueId, venue.Name));
    }

    /// <summary>
    /// PUT /api/venues/{id}: UPDATE a venue and its address (CAR-57). The same checks as POST,
    /// and the name can match the venue's own current name. IsActive true restores a removed venue.
    /// 404 when there's no venue with that id.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = ClubPolicies.Officer)]
    public async Task<ActionResult<VenueDetailDto>> Update(
        int id,
        [FromBody] UpdateVenueRequest input,
        CancellationToken cancellationToken)
    {
        if (await venues.NameExistsAsync(input.Name, id, cancellationToken))
        {
            ModelState.AddModelError(nameof(input.Name), "A venue with this name already exists.");
            return ValidationProblem(ModelState);
        }

        var venue = await venues.UpdateAsync(
            id,
            input.Name,
            input.AddressLine1,
            input.AddressLine2,
            input.City,
            input.State,
            input.ZipCode,
            input.Capacity,
            input.IsActive,
            cancellationToken);

        if (venue is null)
        {
            return NotFound();
        }

        return await ToDetailDtoWithEventsAsync(venue, cancellationToken);
    }

    /// <summary>
    /// DELETE /api/venues/{id}: REMOVE a venue (CAR-58). It's a soft delete, so IsActive becomes
    /// false and the row stays in the database. PUT with IsActive true brings it back.
    /// Removing is allowed even when upcoming events are booked there (CAR-79 decision: warn,
    /// don't block): those events keep their venue and still happen. The officer page warns first.
    /// 204 on success, 404 when there's no venue with that id.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = ClubPolicies.Officer)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var success = await venues.SoftDeleteAsync(id, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    /// <summary>
    /// Looks up one venue's upcoming events, then builds its DTO.
    /// </summary>
    private async Task<VenueDetailDto> ToDetailDtoWithEventsAsync(Venue venue, CancellationToken cancellationToken)
    {
        var upcoming = await venues.ListUpcomingEventsByVenueAsync(venue.VenueId, cancellationToken);
        return ToDetailDto(venue, upcoming[venue.VenueId]);
    }

    /// <summary>
    /// Turns a Venue entity (with its address) and its upcoming events into the DTO sent over the wire.
    /// </summary>
    private static VenueDetailDto ToDetailDto(Venue venue, IEnumerable<Event> upcomingEvents) => new(
        venue.VenueId,
        venue.Name,
        venue.Capacity,
        venue.IsActive,
        venue.Address?.AddressLine1,
        venue.Address?.AddressLine2,
        venue.Address?.City,
        venue.Address?.State,
        venue.Address?.ZipCode,
        upcomingEvents
            .Select(@event => new VenueEventSummaryDto(@event.EventId, @event.Name, @event.StartsAtUtc))
            .ToList());
}
