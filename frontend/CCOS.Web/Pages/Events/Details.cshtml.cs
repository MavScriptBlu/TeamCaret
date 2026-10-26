/// <summary>
/// Details page for a specific event.
/// Displays event information, including date, location, categories, and ticket prices.
/// Provides officer actions for editing or canceling the event.
/// </summary>
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Events;

/// <summary>
/// Event page (/Events/Details/{id}): image, name, date and time, venue, categories,
/// ticket price, RSVP and description. Shows sample data for now.
/// </summary>
public class DetailsModel : PageModel
{
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
    /// Finds the event and works out the price text to show. Sends a 404 when the id is
    /// missing or no active event matches.
    /// </summary>
    /// <param name="id">The event id from the URL.</param>
    public void OnGet(int? id)
    {
        Event = id is int eventId ? SampleEvents.FindActive(eventId) : null;

        // Unknown or canceled event: send a 404. The page shows its "not found" message.
        if (Event is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var regular = TicketPrices.RegularPriceText(Event);
        var member = TicketPrices.MemberPriceText(Event);

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
}
