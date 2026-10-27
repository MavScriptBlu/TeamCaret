using System.Text.Json;
using CCOS.Web.Pages.Events;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Web.Pages.Officer.Events;

/// <summary>
/// Edit Event page (/Officer/Events/Edit/{id}). Loads the event and the venue dropdown from
/// CCOS.Api (CAR-50). Save changes and Cancel both go back to the event page,
/// /Events/Details/{id}; saving an event comes in a later story.
/// Officer-only: lock this down with [Authorize] once login is wired up.
/// </summary>
public class EditModel(EventsApiClient eventsApi, VenuesApiClient venuesApi, ILogger<EditModel> logger)
    : EventFormPageModel
{
    /// <summary>
    /// The event id from the URL, or null when the URL has no id.
    /// </summary>
    public int? Id { get; private set; }

    /// <summary>
    /// True when the id matches an event, so the form can be shown.
    /// </summary>
    public bool Found { get; private set; }

    /// <summary>
    /// True when the API couldn't be reached, so the page shows an error message instead.
    /// </summary>
    public bool LoadFailed { get; private set; }

    public override string Heading => "Edit Event";
    public override string ImagePrompt => "Change image: drag and drop, or click to choose one";
    public override string SaveText => "Save changes";
    public override string CancelPage => "/Events/Details";
    public override int? CancelRouteId => Id;

    /// <summary>
    /// Loads the event and the venues, and fills the form with the event's current values.
    /// Sends a 404 (and leaves Found false, so no form shows) when the id is missing or unknown.
    /// </summary>
    /// <param name="id">The event id from the URL.</param>
    public async Task OnGetAsync(int? id, CancellationToken cancellationToken)
    {
        Id = id;

        try
        {
            var dto = id is int eventId ? await eventsApi.GetAsync(eventId, cancellationToken) : null;
            if (dto is null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var venues = await venuesApi.ListAsync(cancellationToken);
            Venues = venues.Select(venue => new VenueInfo(venue.VenueId, venue.Name)).ToList();

            Found = true;
            Fill(EventMapping.ToEventInfo(dto));
        }
        // Same handling as the product pages: a timeout counts as a failure, but a visitor
        // closing the page (cancellation) is not an error worth logging.
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't load event {EventId} for editing from CCOS.Api.", id);
            LoadFailed = true;
        }
    }

    /// <summary>
    /// Save changes: saving an event comes in a later story, so this goes back to the event page.
    /// </summary>
    public IActionResult OnPost(int? id) => RedirectToPage("/Events/Details", new { id });
}
