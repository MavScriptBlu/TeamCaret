using CCOS.Data.Entities;
using CCOS.Repositories;
using CCOS.Shared;
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
    /// with its address, by name.
    /// TODO: restrict to officers once login exists (CAR-1: Officer only).
    /// </summary>
    [HttpGet("officer")]
    public async Task<IReadOnlyList<VenueDetailDto>> ListForOfficers(CancellationToken cancellationToken)
    {
        var items = await venues.ListAllAsync(cancellationToken);
        return items.Select(ToDetailDto).ToList();
    }

    /// <summary>
    /// GET /api/venues/{id}: one venue with its address, or 404. Removed venues come back too
    /// (IsActive false) so the edit page can restore them.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<VenueDetailDto>> Get(int id, CancellationToken cancellationToken)
    {
        var venue = await venues.FindAsync(id, cancellationToken);

        if (venue is null)
        {
            return NotFound();
        }

        return ToDetailDto(venue);
    }

    /// <summary>
    /// POST /api/venues: CREATE a venue and its address (CAR-51). Missing or too-long fields
    /// and a capacity under 1 come back as 400 automatically ([ApiController]); a name that's
    /// already used comes back as 400 with the message on Name.
    /// TODO: restrict to officers once login exists (CAR-1: Officer only).
    /// </summary>
    [HttpPost]
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
    /// TODO: restrict to officers once login exists (CAR-1: Officer only).
    /// </summary>
    [HttpPut("{id:int}")]
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

        return ToDetailDto(venue);
    }

    /// <summary>
    /// DELETE /api/venues/{id}: REMOVE a venue (CAR-58). It's a soft delete, so IsActive becomes
    /// false and the row stays in the database. PUT with IsActive true brings it back.
    /// 204 on success, 404 when there's no venue with that id.
    /// TODO: restrict to officers once login exists (CAR-1: Officer only).
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var success = await venues.SoftDeleteAsync(id, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    /// <summary>
    /// Turns a Venue entity (with its address) into the DTO sent over the wire.
    /// </summary>
    private static VenueDetailDto ToDetailDto(Venue venue) => new(
        venue.VenueId,
        venue.Name,
        venue.Capacity,
        venue.IsActive,
        venue.Address?.AddressLine1,
        venue.Address?.AddressLine2,
        venue.Address?.City,
        venue.Address?.State,
        venue.Address?.ZipCode);
}
