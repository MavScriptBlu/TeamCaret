using CCOS.Repositories;
using CCOS.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Api.Controllers;

/// <summary>
/// Venue endpoints for the event form's venue dropdown.
/// </summary>
[ApiController]
[Route("api/venues")]
public class VenuesController(VenueRepository venues) : ControllerBase
{
    /// <summary>
    /// GET /api/venues: active venues by name, for the event form's dropdown.
    /// Retired venues can't host new events.
    /// </summary>
    [HttpGet]
    public async Task<IReadOnlyList<VenueDto>> List(CancellationToken cancellationToken)
    {
        var items = await venues.ListActiveAsync(cancellationToken);
        return items.Select(v => new VenueDto(v.VenueId, v.Name)).ToList();
    }
}
