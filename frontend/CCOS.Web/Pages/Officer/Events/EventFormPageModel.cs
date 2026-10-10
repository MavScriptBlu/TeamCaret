using CCOS.Web.Pages.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CCOS.Web.Pages.Officer.Events;

/// <summary>
/// Shared fields for the Create Event and Edit Event pages, rendered by _EventForm.cshtml.
/// Fields follow the Event entity and its ticket type (see "Event, Ticket &amp; Venue
/// Relational Schema" in docs). Nothing is saved or validated yet.
/// Officer-only: lock these pages down with [Authorize] once login is wired up.
/// </summary>
public abstract class EventFormPageModel : PageModel
{
    // Every field is nullable so a new form starts blank instead of showing 0s.

    /// <summary>Event.Name (up to 100 characters).</summary>
    [BindProperty]
    public string? Name { get; set; }

    /// <summary>Event date in club time (America/Chicago).</summary>
    [BindProperty]
    public DateOnly? Date { get; set; }

    /// <summary>Start time in club time. Saved later as Event.StartsAtUtc.</summary>
    [BindProperty]
    public TimeOnly? StartTime { get; set; }

    /// <summary>End time in club time. Saved later as Event.EndsAtUtc.</summary>
    [BindProperty]
    public TimeOnly? EndTime { get; set; }

    /// <summary>Event.VenueId.</summary>
    [BindProperty]
    public int? VenueId { get; set; }

    /// <summary>On the wireframe but not in the database yet.</summary>
    [BindProperty]
    public string? Category { get; set; }

    /// <summary>Regular price of the event's ticket type (Product.Price).</summary>
    [BindProperty]
    public decimal? TicketPrice { get; set; }

    /// <summary>Member price of the event's ticket type (Product.MemberPrice).</summary>
    [BindProperty]
    public decimal? MemberPrice { get; set; }

    /// <summary>Event.TicketCapacity.</summary>
    [BindProperty]
    public int? TicketCapacity { get; set; }

    /// <summary>Event.MembersOnly.</summary>
    [BindProperty]
    public bool MembersOnly { get; set; }

    /// <summary>Event.Description (up to 500 characters, optional).</summary>
    [BindProperty]
    public string? Description { get; set; }

    /// <summary>Page heading, for example "Create Event".</summary>
    public abstract string Heading { get; }

    /// <summary>Text on the image drop area.</summary>
    public abstract string ImagePrompt { get; }

    /// <summary>Text on the save button.</summary>
    public abstract string SaveText { get; }

    /// <summary>Where Cancel goes.</summary>
    public abstract string CancelPage { get; }

    /// <summary>Route id for the Cancel link, or null when the page has none.</summary>
    public virtual int? CancelRouteId => null;

    /// <summary>Venue choices for the Location/Venue dropdown.</summary>
    public IEnumerable<SelectListItem> VenueOptions =>
        SampleVenues.All.Select(v => new SelectListItem(v.Name, v.VenueId.ToString()));

    /// <summary>Category choices for the Categories dropdown.</summary>
    public IEnumerable<SelectListItem> CategoryOptions =>
        SampleCategories.All.Select(c => new SelectListItem(c, c));

    /// <summary>
    /// Fills the form from an existing event, with times shown in club time.
    /// </summary>
    protected void Fill(EventInfo ev)
    {
        var start = ClubTime.ToClubTime(ev.StartsAtUtc);
        var end = ClubTime.ToClubTime(ev.EndsAtUtc);
        var ticket = ev.TicketTypes.FirstOrDefault(t => t.IsActive);

        Name = ev.Name;
        Date = DateOnly.FromDateTime(start);
        StartTime = TimeOnly.FromDateTime(start);
        EndTime = TimeOnly.FromDateTime(end);
        VenueId = ev.VenueId;
        Category = ev.Categories.FirstOrDefault();
        TicketPrice = ticket?.Price;
        MemberPrice = ticket?.MemberPrice;
        TicketCapacity = ev.TicketCapacity;
        MembersOnly = ev.MembersOnly;
        Description = ev.Description;
    }
}
