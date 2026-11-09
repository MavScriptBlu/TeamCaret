using System.ComponentModel.DataAnnotations;
using CCOS.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Officer.Venues;

/// <summary>
/// Shared fields and checks for the Venue Form pages (Create and Edit), shown by _VenueForm.cshtml.
/// Fields follow the Venue and VenueAddress tables (see "Event, Ticket &amp; Venue
/// Relational Schema" in docs). The rules match CreateVenueRequest and UpdateVenueRequest in
/// CCOS.Shared, so the form and the API say the same thing. The image and description are
/// display-only for now.
/// Officer-only: lock these pages down with [Authorize] once login is wired up.
/// </summary>
public abstract class VenueFormPageModel : PageModel
{
    // Every field is nullable so the form starts blank instead of showing 0s.

    /// <summary>Venue.Name (up to 100 characters, unique).</summary>
    [BindProperty]
    [Required(ErrorMessage = "Venue name is required.")]
    [StringLength(100, ErrorMessage = "Venue name can be at most 100 characters.")]
    public string? Name { get; set; }

    /// <summary>VenueAddress.AddressLine1 (up to 100 characters).</summary>
    [BindProperty]
    [Required(ErrorMessage = "Street address is required.")]
    [StringLength(100, ErrorMessage = "Street address can be at most 100 characters.")]
    public string? AddressLine1 { get; set; }

    /// <summary>VenueAddress.AddressLine2 (up to 100 characters, optional).</summary>
    [BindProperty]
    [StringLength(100, ErrorMessage = "Building or room can be at most 100 characters.")]
    public string? AddressLine2 { get; set; }

    /// <summary>VenueAddress.City (up to 50 characters).</summary>
    [BindProperty]
    [Required(ErrorMessage = "City is required.")]
    [StringLength(50, ErrorMessage = "City can be at most 50 characters.")]
    public string? City { get; set; }

    /// <summary>VenueAddress.State (2-letter code).</summary>
    [BindProperty]
    [Required(ErrorMessage = "State is required.")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "Use the two-letter state code, like WI.")]
    public string? State { get; set; }

    /// <summary>VenueAddress.ZipCode (5 digits, or ZIP+4 like 54481-1234, CAR-63).</summary>
    [BindProperty]
    [Required(ErrorMessage = "ZIP code is required.")]
    [StringLength(10, ErrorMessage = "ZIP code can be at most 10 characters.")]
    [RegularExpression(ZipCodeRules.Pattern, ErrorMessage = ZipCodeRules.ErrorMessage)]
    public string? ZipCode { get; set; }

    /// <summary>Venue.Capacity (more than 0).</summary>
    [BindProperty]
    [Required(ErrorMessage = "Capacity is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be at least 1.")]
    public int? Capacity { get; set; }

    /// <summary>
    /// Venue.IsActive. Unticking it removes the venue and ticking it restores it (CAR-58).
    /// Only the Edit page shows this box. Create always makes an active venue.
    /// </summary>
    [BindProperty]
    public bool IsActive { get; set; } = true;

    /// <summary>On the wireframe but not in the database yet.</summary>
    [BindProperty]
    public string? Description { get; set; }

    /// <summary>Page heading, for example "Venue Form".</summary>
    public virtual string Heading => "Venue Form";

    /// <summary>Text on the image drop area.</summary>
    public virtual string ImagePrompt => "Drag and drop an image, or click to choose one";

    /// <summary>Text on the save button.</summary>
    public virtual string SaveText => "Save";

    /// <summary>Where Cancel goes.</summary>
    public virtual string CancelPage => "/Events/Index";

    /// <summary>True when the form shows the "Show in the venue list" box (Edit only).</summary>
    public virtual bool ShowActiveBox => false;
}
