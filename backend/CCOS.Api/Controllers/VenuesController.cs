using CCOS.Repositories;
using CCOS.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Api.Controllers;

/// <summary>
/// Venue endpoints. The website's venue form saves through POST, and the event form's
/// venue dropdown reads from GET.
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
        if (await venues.NameExistsAsync(input.Name, cancellationToken))
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
}
