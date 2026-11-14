using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Extensions;
using CCOS.Web.Pages.Events;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CCOS.Web.Pages.Officer.Events;

/// <summary>
/// Shared fields and checks for the Create Event and Edit Event pages, rendered by _EventForm.cshtml.
/// Fields follow the Event entity and its ticket type (see "Event, Ticket &amp; Venue
/// Relational Schema" in docs). The rules match CreateEventRequest and UpdateEventRequest in
/// CCOS.Shared, so the form and the API say the same thing (CAR-55).
/// Officer-only: locked by AuthorizeFolder("/Officer") in Program.cs (CAR-69).
/// </summary>
public abstract class EventFormPageModel : PageModel
{
    // Every field is nullable so a new form starts blank instead of showing 0s.

    /// <summary>Event.Name (up to 100 characters).</summary>
    [BindProperty]
    [Required(ErrorMessage = "Event name is required.")]
    [StringLength(100, ErrorMessage = "Event name can be at most 100 characters.")]
    public string? Name { get; set; }

    /// <summary>Event date in club time (America/Chicago).</summary>
    [BindProperty]
    [Required(ErrorMessage = "Pick a date.")]
    public DateOnly? Date { get; set; }

    /// <summary>Start time in club time. Saved as Event.StartsAtUtc.</summary>
    [BindProperty]
    [Required(ErrorMessage = "Pick a start time.")]
    public TimeOnly? StartTime { get; set; }

    /// <summary>End time in club time. Saved as Event.EndsAtUtc.</summary>
    [BindProperty]
    [Required(ErrorMessage = "Pick an end time.")]
    public TimeOnly? EndTime { get; set; }

    /// <summary>Event.VenueId.</summary>
    [BindProperty]
    [Required(ErrorMessage = "Choose a venue.")]
    public int? VenueId { get; set; }

    /// <summary>On the wireframe but not in the database yet, so it isn't saved.</summary>
    [BindProperty]
    public string? Category { get; set; }

    /// <summary>Regular price of the event's ticket type (Product.Price).</summary>
    [BindProperty]
    [Required(ErrorMessage = "Ticket price is required (use 0 for free).")]
    [Range(0, 99999999.99, ErrorMessage = "Ticket price must be at least 0.")]
    public decimal? TicketPrice { get; set; }

    /// <summary>Member price of the event's ticket type (Product.MemberPrice).</summary>
    [BindProperty]
    [Required(ErrorMessage = "Member price is required (use 0 for free).")]
    [Range(0, 99999999.99, ErrorMessage = "Member price must be at least 0.")]
    public decimal? MemberPrice { get; set; }

    /// <summary>Event.TicketCapacity.</summary>
    [BindProperty]
    [Required(ErrorMessage = "Capacity is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be at least 1.")]
    public int? TicketCapacity { get; set; }

    /// <summary>Event.MembersOnly.</summary>
    [BindProperty]
    public bool MembersOnly { get; set; }

    /// <summary>Event.Description (up to 500 characters, optional).</summary>
    [BindProperty]
    [StringLength(500, ErrorMessage = "Description can be at most 500 characters.")]
    public string? Description { get; set; }

    /// <summary>
    /// Event.IsActive. Ticked means the event is shown, unticked removes it (CAR-56). New events
    /// start ticked. Only the Edit page shows the checkbox.
    /// </summary>
    [BindProperty]
    public bool IsActive { get; set; } = true;

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

    /// <summary>True on the Edit page, which shows the "Show in the event list" checkbox.</summary>
    public virtual bool ShowActiveBox => false;

    /// <summary>
    /// Venues for the dropdown. Create Event and Edit Event load the real venues from the
    /// database (CAR-51, CAR-50).
    /// </summary>
    protected IReadOnlyList<VenueInfo> Venues { get; set; } = [];

    /// <summary>Venue choices for the Location/Venue dropdown.</summary>
    public IEnumerable<SelectListItem> VenueOptions =>
        Venues.Select(venue => new SelectListItem(venue.Name, venue.VenueId.ToString()));

    /// <summary>Category choices for the Categories dropdown.</summary>
    public IEnumerable<SelectListItem> CategoryOptions =>
        SampleCategories.All.Select(category => new SelectListItem(category, category));

    /// <summary>
    /// Fills the form from an existing event, with times shown in club time.
    /// </summary>
    protected void Fill(EventInfo eventInfo)
    {
        // Times are stored in UTC, but officers think in club time, so convert before filling the form.
        var startsAt = ClubTime.ToClubTime(eventInfo.StartsAtUtc);
        var endsAt = ClubTime.ToClubTime(eventInfo.EndsAtUtc);

        // The form has one price box, so use the first ticket type that is still on sale.
        var ticketType = eventInfo.TicketTypes.FirstOrDefault(candidate => candidate.IsActive);

        Name = eventInfo.Name;
        Date = DateOnly.FromDateTime(startsAt);
        StartTime = TimeOnly.FromDateTime(startsAt);
        EndTime = TimeOnly.FromDateTime(endsAt);
        VenueId = eventInfo.VenueId;
        Category = eventInfo.Categories.FirstOrDefault();
        TicketPrice = ticketType?.Price;
        MemberPrice = ticketType?.MemberPrice;
        TicketCapacity = eventInfo.TicketCapacity;
        MembersOnly = eventInfo.MembersOnly;
        Description = eventInfo.Description;
        IsActive = eventInfo.IsActive;
    }

    /// <summary>
    /// Loads the venue dropdown. If the API can't be reached the dropdown is left empty
    /// (the "Add a venue" link still works).
    /// </summary>
    protected async Task LoadVenuesAsync(VenuesApiClient venuesApi, ILogger logger, CancellationToken cancellationToken)
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
    /// The rules that look at more than one field: the end has to be after the start, and the
    /// member price can't be more than the ticket price. Adds a message next to the field when
    /// one fails. Run this after the single-field rules pass.
    /// </summary>
    /// <returns>The start and end in UTC, or null when something is wrong (the messages are already added).</returns>
    protected (DateTime StartsAtUtc, DateTime EndsAtUtc)? CheckAcrossFields()
    {
        // The Required rules guarantee these have values, so .Value is safe once ModelState is valid.
        var startsAtUtc = ClubTime.ToUtc(Date!.Value, StartTime!.Value);
        var endsAtUtc = ClubTime.ToUtc(Date.Value, EndTime!.Value);

        if (startsAtUtc is null)
        {
            ModelState.AddModelError(nameof(StartTime), "That time doesn't exist on this date (the clocks skip ahead). Pick another.");
        }

        if (endsAtUtc is null)
        {
            ModelState.AddModelError(nameof(EndTime), "That time doesn't exist on this date (the clocks skip ahead). Pick another.");
        }

        if (startsAtUtc is not null && endsAtUtc is not null && endsAtUtc <= startsAtUtc)
        {
            ModelState.AddModelError(nameof(EndTime), "The event has to end after it starts.");
        }

        if (MemberPrice > TicketPrice)
        {
            ModelState.AddModelError(nameof(MemberPrice), "Member price cannot exceed regular price.");
        }

        return ModelState.IsValid ? (startsAtUtc!.Value, endsAtUtc!.Value) : null;
    }

    /// <summary>
    /// Shows the API's validation messages next to the matching form fields. The API names a few
    /// fields differently from the form (EndsAtUtc is the End time box, and so on), so those are
    /// renamed on the way in.
    /// </summary>
    protected async Task ShowApiErrorsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var fromApi = new ModelStateDictionary();
        await fromApi.AddApiValidationErrorsAsync(
            response,
            string.Empty,
            "The event could not be saved. Please check the values and try again.",
            cancellationToken);

        foreach (var (key, entry) in fromApi)
        {
            var formKey = key switch
            {
                "StartsAtUtc" => nameof(StartTime),
                "EndsAtUtc" => nameof(EndTime),
                "MemberTicketPrice" => nameof(MemberPrice),
                _ => key,
            };

            foreach (var error in entry.Errors)
            {
                ModelState.AddModelError(formKey, error.ErrorMessage);
            }
        }
    }
}
