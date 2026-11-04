using System.Net;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Pages.Events;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Web.Pages.Officer.Events;

/// <summary>
/// Edit Event page (/Officer/Events/Edit/{id}). Loads the event and the venue dropdown from
/// CCOS.Api (CAR-50), checks what the officer typed, and sends the changes to PUT /api/events/{id}
/// (CAR-55). Save goes back to the event page with an "Event saved" message. The "Show in the
/// event list" box removes (unticked) or restores (ticked) the event (CAR-56). Removed events
/// open here too, so they can be restored.
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
    public override bool ShowActiveBox => true;

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
            var dto = id is int eventId ? await eventsApi.GetForOfficerAsync(eventId, cancellationToken) : null;
            if (dto is null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var venues = await venuesApi.ListAsync(cancellationToken);
            Venues = venues.Select(venue => new VenueInfo(venue.VenueId, venue.Name)).ToList();

            // A removed event may use a venue that's no longer in the dropdown. Keep it listed
            // so the form doesn't silently switch venues when the officer restores the event.
            if (Venues.All(venue => venue.VenueId != dto.VenueId))
            {
                Venues = [.. Venues, new VenueInfo(dto.VenueId, dto.VenueName)];
            }

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
    /// Save changes: redisplays the form with messages if the input is invalid or the API call
    /// fails, and goes back to the event page (or the event list, when the event was removed).
    /// </summary>
    /// <param name="id">The event id from the URL.</param>
    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken cancellationToken)
    {
        Id = id;

        // The form is only on screen for an event that exists, so keep showing it after an error.
        Found = true;

        if (id is not int eventId)
        {
            Found = false;
            Response.StatusCode = StatusCodes.Status404NotFound;
            return Page();
        }

        // Single-field rules first, then the ones that compare fields (end after start, prices).
        var times = ModelState.IsValid ? CheckAcrossFields() : null;

        if (times is null)
        {
            await LoadVenuesForRedisplayAsync(cancellationToken);
            return Page();
        }

        // The Required rules guarantee these have values, so ! and .Value are safe.
        var request = new UpdateEventRequest(
            Name!,
            Description,
            times.Value.StartsAtUtc,
            times.Value.EndsAtUtc,
            VenueId!.Value,
            TicketCapacity!.Value,
            MembersOnly,
            TicketPrice!.Value,
            MemberPrice!.Value,
            IsActive);

        try
        {
            using var response = await eventsApi.UpdateAsync(eventId, request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Found = false;
                Response.StatusCode = StatusCodes.Status404NotFound;
                return Page();
            }

            // The API rejected the values (for example, a capacity below the tickets already sold):
            // show its messages next to the matching fields.
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
            {
                await ShowApiErrorsAsync(response, cancellationToken);
                await LoadVenuesForRedisplayAsync(cancellationToken);
                return Page();
            }

            // Someone else saved this event while it was open here.
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Someone else changed this event while you were editing. Reload the page and try again.");
                await LoadVenuesForRedisplayAsync(cancellationToken);
                return Page();
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Failed to update event {EventId}. Status code: {StatusCode}", eventId, response.StatusCode);
                ModelState.AddModelError(string.Empty, "The event could not be saved. Please try again.");
                await LoadVenuesForRedisplayAsync(cancellationToken);
                return Page();
            }
        }
        // The API is down or unreachable.
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The event API could not be reached while saving event {EventId}", eventId);
            ModelState.AddModelError(string.Empty, "The event API could not be reached. Please try again later.");
            await LoadVenuesForRedisplayAsync(cancellationToken);
            return Page();
        }
        // The API took too long. (A visitor closing the page is not treated as an error.)
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The event API request timed out while saving event {EventId}", eventId);
            ModelState.AddModelError(string.Empty, "The event API request timed out. Please try again later.");
            await LoadVenuesForRedisplayAsync(cancellationToken);
            return Page();
        }

        // A removed event has no public page anymore, so go to the officer list instead.
        if (!IsActive)
        {
            TempData["EventRemoved"] = $"Event removed: {Name!.Trim()}. You can restore it from the event list.";
            return RedirectToPage("/Officer/Events/Index", new { show = "inactive" });
        }

        TempData["EventSaved"] = $"Event saved: {Name!.Trim()}";
        return RedirectToPage("/Events/Details", new { id = eventId });
    }

    /// <summary>
    /// Reloads the venue dropdown when the form is shown again after an error.
    /// </summary>
    private async Task LoadVenuesForRedisplayAsync(CancellationToken cancellationToken)
    {
        await LoadVenuesAsync(venuesApi, logger, cancellationToken);
    }
}
