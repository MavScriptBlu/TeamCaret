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

    public void OnGet(int? id)
    {
        Event = id is int eventId ? SampleEvents.FindActive(eventId) : null;

        if (Event is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var regular = TicketPrices.RegularPriceText(Event);
        var member = TicketPrices.MemberPriceText(Event);

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
