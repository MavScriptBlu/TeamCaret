using System.Text.Json;
using CCOS.Web.Pages.Events;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Web.Pages.Officer.Events;

/// <summary>
/// Create Event page (/Officer/Events/Create). Save and Cancel both go back to /Events.
/// The venue dropdown lists the real venues from CCOS.Api (CAR-51); the event itself is
/// not saved or validated yet.
/// Officer-only: lock this down with [Authorize] once login is wired up.
/// </summary>
public class CreateModel(VenuesApiClient venuesApi, ILogger<CreateModel> logger) : EventFormPageModel
{
    public override string Heading => "Create Event";
    public override string ImagePrompt => "Drag and drop an image, or click to choose one";
    public override string SaveText => "Save";
    public override string CancelPage => "/Events/Index";

    /// <summary>
    /// Shows the blank form with the real venues in the dropdown. If the API can't be reached,
    /// the dropdown is left empty (the "Add a venue" link still works).
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var venues = await venuesApi.ListAsync(cancellationToken);
            Venues = venues.Select(venue => new VenueInfo(venue.VenueId, venue.Name)).ToList();
        }
        // Same handling as the product pages: a timeout counts as a failure, but a visitor
        // closing the page (cancellation) is not an error worth logging.
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't load venues from CCOS.Api.");
            Venues = [];
        }
    }

    /// <summary>
    /// Save: nothing is saved yet, so this just goes back to the Events page.
    /// </summary>
    public IActionResult OnPost() => RedirectToPage("/Events/Index");
}
