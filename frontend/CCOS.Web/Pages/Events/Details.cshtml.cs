using System.Net;
using System.Text.Json;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Events;

/// <summary>
/// Event page (/Events/Details/{id}): image, name, date and time, venue, categories,
/// ticket price, RSVP and description. Loads the event from CCOS.Api (CAR-50). The officer
/// menu can remove the event (CAR-56): it is only hidden (soft-deleted) and can be restored.
/// </summary>
public class DetailsModel(EventsApiClient eventsApi, ILogger<DetailsModel> logger) : PageModel
{
    private static readonly Action<ILogger, int, Exception?> EventRemoveTimeoutLog =
        LoggerMessage.Define<int>(LogLevel.Error, new EventId(1, nameof(EventRemoveTimeoutLog)),
            "The event API request timed out while removing event {EventId}");

    private static readonly Action<ILogger, int, Exception?> EventRemoveUnreachableLog =
        LoggerMessage.Define<int>(LogLevel.Error, new EventId(2, nameof(EventRemoveUnreachableLog)),
            "The event API could not be reached while removing event {EventId}");

    private static readonly Action<ILogger, int?, Exception?> EventLoadFailedLog =
        LoggerMessage.Define<int?>(LogLevel.Error, new EventId(3, nameof(EventLoadFailedLog)),
            "Couldn't load event {EventId} from CCOS.Api.");

    private static readonly Action<ILogger, int, HttpStatusCode, Exception?> EventRemoveFailedLog =
        LoggerMessage.Define<int, HttpStatusCode>(LogLevel.Error, new EventId(4, nameof(EventRemoveFailedLog)),
            "Failed to remove event {EventId}. Status code: {StatusCode}");

    /// <summary>
    /// The event to show, or null when no active event matches the id.
    /// </summary>
    public EventInfo? Event { get; private set; }

    /// <summary>
    /// Regular ticket price, for example "$10.00", "from $5.00" or "Free".
    /// Null for members-only events and when no tickets are on sale.
    /// </summary>
    public string? PriceText { get; private set; }

    /// <summary>
    /// Member ticket price. Shown when it differs from the regular price,
    /// and always for members-only events. Null when no tickets are on sale.
    /// </summary>
    public string? MemberPriceText { get; private set; }

    /// <summary>
    /// True when there is a member price the visitor can't see (not a current member, CAR-69).
    /// The page shows "Log in as a member to see the member price" instead.
    /// </summary>
    public bool MemberPriceHidden { get; private set; }

    /// <summary>
    /// True when the API couldn't be reached, so the page shows an error message instead.
    /// </summary>
    public bool LoadFailed { get; private set; }

    /// <summary>
    /// Loads the event and works out the price text to show. Sends a 404 when the id is
    /// missing or no active event matches, and shows an error message when the API fails.
    /// </summary>
    /// <param name="id">The event id from the URL.</param>
    public async Task OnGetAsync(int? id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = id is int eventId ? await eventsApi.GetAsync(eventId, cancellationToken) : null;
            Event = dto is null ? null : EventMapping.ToEventInfo(dto);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            EventLoadFailedLog(logger, id, ex);
            LoadFailed = true;
            return;
        }

        // Unknown or canceled event: send a 404. The page shows its "not found" message.
        if (Event is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var regular = TicketPrices.RegularPriceText(Event);
        var member = TicketPrices.MemberPriceText(Event);
        MemberPriceHidden = TicketPrices.MemberPriceHidden(Event);

        // Members-only events show just the member price. Other events show the regular price,
        // plus the member price only when it is actually different.
        if (Event.MembersOnly)
        {
            // Only members can buy tickets, so the regular price doesn't apply.
            MemberPriceText = member;
        }
        else
        {
            PriceText = regular;
            MemberPriceText = member != regular ? member : null;
        }
    }

    /// <summary>
    /// The Remove event button (after the officer says yes in the dialog). Soft-removes the event
    /// through DELETE /api/events/{id}, then goes to the Events page. The row stays in the database.
    /// </summary>
    /// <param name="id">The event id from the URL.</param>
    public async Task<IActionResult> OnPostRemoveAsync(int? id, CancellationToken cancellationToken)
    {
        if (id is not int eventId)
        {
            TempData["EventError"] = "The event could not be identified. Please try again.";
            return RedirectToPage("/Events/Index");
        }

        try
        {
            using var response = await eventsApi.DeleteAsync(eventId, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                TempData["EventError"] = "That event could not be found. It may already be gone.";
                return RedirectToPage("/Events/Index");
            }

            if (!response.IsSuccessStatusCode)
            {
                EventRemoveFailedLog(logger, eventId, response.StatusCode, null);
                TempData["EventError"] = "The event could not be removed. Please try again.";
                return RedirectToPage("/Events/Details", new { id = eventId });
            }
        }
        catch (HttpRequestException ex)
        {
            EventRemoveUnreachableLog(logger, eventId, ex);
            TempData["EventError"] = "The event API could not be reached. Please try again later.";
            return RedirectToPage("/Events/Details", new { id = eventId });
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            EventRemoveTimeoutLog(logger, eventId, ex);
            TempData["EventError"] = "The event API request timed out. Please try again later.";
            return RedirectToPage("/Events/Details", new { id = eventId });
        }

        TempData["EventRemoved"] = "Event removed. You can restore it from the officer event list.";
        return RedirectToPage("/Events/Index");
    }
}
