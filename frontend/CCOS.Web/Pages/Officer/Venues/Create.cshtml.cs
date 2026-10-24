using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Officer.Venues;

/// <summary>
/// Venue form page (/Officer/Venues/Create). Save and Cancel both go back to /Events.
/// Fields follow the Venue and VenueAddress tables (see "Event, Ticket &amp; Venue
/// Relational Schema" in docs). Nothing is saved or validated yet.
/// Officer-only: lock this down with [Authorize] once login is wired up.
/// </summary>
public class CreateModel : PageModel
{
    // Every field is nullable so the form starts blank instead of showing 0s.

    /// <summary>Venue.Name (up to 100 characters, unique).</summary>
    [BindProperty]
    public string? Name { get; set; }

    /// <summary>VenueAddress.AddressLine1 (up to 100 characters).</summary>
    [BindProperty]
    public string? AddressLine1 { get; set; }

    /// <summary>VenueAddress.AddressLine2 (up to 100 characters, optional).</summary>
    [BindProperty]
    public string? AddressLine2 { get; set; }

    /// <summary>VenueAddress.City (up to 50 characters).</summary>
    [BindProperty]
    public string? City { get; set; }

    /// <summary>VenueAddress.State (2-letter code).</summary>
    [BindProperty]
    public string? State { get; set; }

    /// <summary>VenueAddress.ZipCode (up to 10 characters).</summary>
    [BindProperty]
    public string? ZipCode { get; set; }

    /// <summary>Venue.Capacity (more than 0).</summary>
    [BindProperty]
    public int? Capacity { get; set; }

    /// <summary>On the wireframe but not in the database yet.</summary>
    [BindProperty]
    public string? Description { get; set; }

    /// <summary>Shows the blank form. Nothing to load.</summary>
    public void OnGet()
    {
    }

    /// <summary>
    /// Save: nothing is saved yet, so this just goes back to the Events page.
    /// </summary>
    public IActionResult OnPost() => RedirectToPage("/Events/Index");
}
