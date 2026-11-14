using System.Net;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Officer.Venues;

/// <summary>
/// Officer venue list (/Officer/Venues). Shows every venue from CCOS.Api, removed ones too
/// (CAR-57), with Remove and Restore buttons (CAR-58). Venues are never deleted: Remove sets
/// IsActive to false and Restore sets it back to true, and the Remove button asks first.
/// Officer-only: locked by AuthorizeFolder("/Officer") in Program.cs (CAR-69).
/// </summary>
public class IndexModel(VenuesApiClient venuesApi, ILogger<IndexModel> logger) : PageModel
{
    /// <summary>
    /// Filter from the URL: "active", "inactive", or empty for all.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Show { get; set; }

    /// <summary>
    /// Venue row to open after returning from an action.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int? ExpandedId { get; set; }

    /// <summary>
    /// Venues to show after filtering.
    /// </summary>
    public IReadOnlyList<VenueDetailDto> Venues { get; private set; } = [];

    /// <summary>
    /// Number of venues before filtering. Zero means there are no venues yet.
    /// </summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// True when the API couldn't be reached, so the page shows an error message instead.
    /// </summary>
    public bool LoadFailed { get; private set; }

    /// <summary>
    /// Loads every venue (active and removed), then applies the Show filter from the URL.
    /// If the API can't be reached, LoadFailed is set so the page shows an error message.
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<VenueDetailDto> all;
        try
        {
            all = await venuesApi.ListAllAsync(cancellationToken);
        }
        // Only handle "API is down or sent bad data" problems. A timeout counts, but a visitor
        // closing the page (cancellation) is not an error worth logging.
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't load venues from CCOS.Api.");
            LoadFailed = true;
            return;
        }

        // The count is taken before filtering so the page can tell "no venues yet"
        // apart from "no venues match this filter".
        TotalCount = all.Count;
        Venues = Show switch
        {
            "active" => all.Where(venue => venue.IsActive).ToList(),
            "inactive" => all.Where(venue => !venue.IsActive).ToList(),
            _ => all,
        };
    }

    /// <summary>
    /// The Remove button (after the officer says yes in the dialog). Soft-removes the venue
    /// through DELETE /api/venues/{id}. The row stays in the database.
    /// </summary>
    public async Task<IActionResult> OnPostRemoveAsync(int id, string? show, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            TempData["VenueError"] = "The venue could not be identified. Please refresh the list and try again.";
            return RedirectToList(show, id);
        }

        try
        {
            using var response = await venuesApi.DeleteAsync(id, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                TempData["VenueError"] = "That venue could not be found. Refresh the list and try again.";
                return RedirectToList(show, id);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to remove venue {VenueId}. Status code: {StatusCode}", id, response.StatusCode);
                TempData["VenueError"] = "The venue could not be removed. Please try again.";
                return RedirectToList(show, id);
            }

            TempData["VenueRemoved"] = "Venue removed. You can restore it from this list.";

            // A removed venue disappears from the "In the venue list" view, so show the removed ones.
            return RedirectToList(show == "active" ? "inactive" : show, id);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The venue API could not be reached while removing venue {VenueId}", id);
            TempData["VenueError"] = "The venue API could not be reached. Please try again later.";
            return RedirectToList(show, id);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The venue API request timed out while removing venue {VenueId}", id);
            TempData["VenueError"] = "The venue API request timed out. Please try again later.";
            return RedirectToList(show, id);
        }
    }

    /// <summary>
    /// The Restore button. Loads the venue, then saves it again with IsActive true through
    /// PUT /api/venues/{id}. A venue with no address can't be saved by the API, so those are
    /// sent to the Edit page to fill the address in first.
    /// </summary>
    public async Task<IActionResult> OnPostRestoreAsync(int id, string? show, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            TempData["VenueError"] = "The venue could not be identified. Please refresh the list and try again.";
            return RedirectToList(show, id);
        }

        try
        {
            var venue = await venuesApi.GetAsync(id, cancellationToken);
            if (venue is null)
            {
                TempData["VenueError"] = "That venue could not be found. Refresh the list and try again.";
                return RedirectToList(show, id);
            }

            // The API needs a full address on every save. Venues made before addresses existed
            // don't have one, so the officer fills it in on the Edit page (and ticks the box there).
            if (venue.AddressLine1 is null || venue.City is null || venue.State is null || venue.ZipCode is null)
            {
                TempData["VenueError"] = $"{venue.Name} has no address yet. Open Edit to add one, then restore it.";
                return RedirectToList(show, id);
            }

            var request = new UpdateVenueRequest(
                venue.Name,
                venue.AddressLine1,
                venue.AddressLine2,
                venue.City,
                venue.State,
                venue.ZipCode,
                venue.Capacity,
                IsActive: true);

            using var response = await venuesApi.UpdateAsync(id, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to restore venue {VenueId}. Status code: {StatusCode}", id, response.StatusCode);
                TempData["VenueError"] = "The venue could not be restored. Open Edit to check its details.";
                return RedirectToList(show, id);
            }

            TempData["VenueRestored"] = $"Venue restored: {venue.Name}";

            // A restored venue disappears from the "Removed" view, so show the ones in the list.
            return RedirectToList(show == "inactive" ? "active" : show, id);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The venue API could not be reached while restoring venue {VenueId}", id);
            TempData["VenueError"] = "The venue API could not be reached. Please try again later.";
            return RedirectToList(show, id);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The venue API request timed out while restoring venue {VenueId}", id);
            TempData["VenueError"] = "The venue API request timed out. Please try again later.";
            return RedirectToList(show, id);
        }
    }

    private IActionResult RedirectToList(string? show, int? expandedId)
    {
        var filter = show is "active" or "inactive" ? show : null;
        return RedirectToPage("/Officer/Venues/Index", new { show = filter, expandedId });
    }
}
