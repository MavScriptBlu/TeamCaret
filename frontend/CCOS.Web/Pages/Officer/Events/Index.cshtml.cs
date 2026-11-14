using System.Net;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Officer.Events;

/// <summary>
/// Officer event list (/Officer/Events). Shows every event from CCOS.Api, removed and past ones
/// too (CAR-56), with Remove and Restore buttons. Events are never deleted: Remove sets IsActive
/// to false and Restore sets it back to true, and the Remove button asks first.
/// Officer-only: locked by AuthorizeFolder("/Officer") in Program.cs (CAR-69).
/// </summary>
public class IndexModel(EventsApiClient eventsApi, ILogger<IndexModel> logger) : PageModel
{
    /// <summary>
    /// Filter from the URL: "active", "inactive", or empty for all.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Show { get; set; }

    /// <summary>
    /// Event row to open after returning from an action.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int? ExpandedId { get; set; }

    /// <summary>
    /// Events to show after filtering.
    /// </summary>
    public IReadOnlyList<EventDto> Events { get; private set; } = [];

    /// <summary>
    /// Number of events before filtering. Zero means there are no events yet.
    /// </summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// True when the API couldn't be reached, so the page shows an error message instead.
    /// </summary>
    public bool LoadFailed { get; private set; }

    /// <summary>
    /// Loads every event (active and removed), then applies the Show filter from the URL.
    /// If the API can't be reached, LoadFailed is set so the page shows an error message.
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<EventDto> all;
        try
        {
            all = await eventsApi.ListAllAsync(cancellationToken);
        }
        // Only handle "API is down or sent bad data" problems. A timeout counts, but a visitor
        // closing the page (cancellation) is not an error worth logging.
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't load events from CCOS.Api.");
            LoadFailed = true;
            return;
        }

        // The count is taken before filtering so the page can tell "no events yet"
        // apart from "no events match this filter".
        TotalCount = all.Count;
        Events = Show switch
        {
            "active" => all.Where(item => item.IsActive).ToList(),
            "inactive" => all.Where(item => !item.IsActive).ToList(),
            _ => all,
        };
    }

    /// <summary>
    /// The Remove button (after the officer says yes in the dialog). Soft-removes the event
    /// through DELETE /api/events/{id}. The row stays in the database.
    /// </summary>
    public async Task<IActionResult> OnPostRemoveAsync(int id, string? show, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            TempData["EventError"] = "The event could not be identified. Please refresh the list and try again.";
            return RedirectToList(show, id);
        }

        try
        {
            using var response = await eventsApi.DeleteAsync(id, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                TempData["EventError"] = "That event could not be found. Refresh the list and try again.";
                return RedirectToList(show, id);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to remove event {EventId}. Status code: {StatusCode}", id, response.StatusCode);
                TempData["EventError"] = "The event could not be removed. Please try again.";
                return RedirectToList(show, id);
            }

            TempData["EventRemoved"] = "Event removed. You can restore it from this list.";

            // A removed event disappears from the "In the event list" view, so show the removed ones.
            return RedirectToList(show == "active" ? "inactive" : show, id);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The event API could not be reached while removing event {EventId}", id);
            TempData["EventError"] = "The event API could not be reached. Please try again later.";
            return RedirectToList(show, id);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The event API request timed out while removing event {EventId}", id);
            TempData["EventError"] = "The event API request timed out. Please try again later.";
            return RedirectToList(show, id);
        }
    }

    /// <summary>
    /// The Restore button. Loads the event, then saves it again with IsActive true through
    /// PUT /api/events/{id}. The API wants a ticket price on every save, so an event with no
    /// ticket on sale is sent to the Edit page to set one first.
    /// </summary>
    public async Task<IActionResult> OnPostRestoreAsync(int id, string? show, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            TempData["EventError"] = "The event could not be identified. Please refresh the list and try again.";
            return RedirectToList(show, id);
        }

        try
        {
            var existing = await eventsApi.GetForOfficerAsync(id, cancellationToken);
            if (existing is null)
            {
                TempData["EventError"] = "That event could not be found. Refresh the list and try again.";
                return RedirectToList(show, id);
            }

            var ticketType = existing.TicketTypes.FirstOrDefault(candidate => candidate.IsActive);
            if (ticketType is null)
            {
                TempData["EventError"] = $"{existing.Name} has no ticket price yet. Open Edit to set one, then restore it.";
                return RedirectToList(show, id);
            }

            var request = new UpdateEventRequest(
                existing.Name,
                existing.Description,
                existing.StartsAtUtc,
                existing.EndsAtUtc,
                existing.VenueId,
                existing.TicketCapacity,
                existing.MembersOnly,
                ticketType.Price,
                // The officer endpoint always sends the member price.
                ticketType.MemberPrice.GetValueOrDefault(),
                IsActive: true);

            using var response = await eventsApi.UpdateAsync(id, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to restore event {EventId}. Status code: {StatusCode}", id, response.StatusCode);
                TempData["EventError"] = "The event could not be restored. Open Edit to check its details.";
                return RedirectToList(show, id);
            }

            TempData["EventRestored"] = $"Event restored: {existing.Name}";

            // A restored event disappears from the "Removed" view, so show the ones in the list.
            return RedirectToList(show == "inactive" ? "active" : show, id);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The event API could not be reached while restoring event {EventId}", id);
            TempData["EventError"] = "The event API could not be reached. Please try again later.";
            return RedirectToList(show, id);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The event API request timed out while restoring event {EventId}", id);
            TempData["EventError"] = "The event API request timed out. Please try again later.";
            return RedirectToList(show, id);
        }
    }

    private IActionResult RedirectToList(string? show, int? expandedId)
    {
        var filter = show is "active" or "inactive" ? show : null;
        return RedirectToPage("/Officer/Events/Index", new { show = filter, expandedId });
    }
}
