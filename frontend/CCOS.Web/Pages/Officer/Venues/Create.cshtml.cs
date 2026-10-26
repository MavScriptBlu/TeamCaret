using System.ComponentModel.DataAnnotations;
using System.Net;
using CCOS.Shared;
using CCOS.Web.Extensions;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Officer.Venues;

/// <summary>
/// Venue form page (/Officer/Venues/Create). Checks what the officer typed, sends the new venue
/// to CCOS.Api (CAR-51), then goes to /Events with a "Venue added" message.
/// Fields follow the Venue and VenueAddress tables (see "Event, Ticket &amp; Venue
/// Relational Schema" in docs). The image and description are display-only for now.
/// Officer-only: lock this down with [Authorize] once login is wired up.
/// </summary>
public class CreateModel(VenuesApiClient venuesApi, ILogger<CreateModel> logger) : PageModel
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

    /// <summary>VenueAddress.ZipCode (up to 10 characters).</summary>
    [BindProperty]
    [Required(ErrorMessage = "ZIP code is required.")]
    [StringLength(10, ErrorMessage = "ZIP code can be at most 10 characters.")]
    public string? ZipCode { get; set; }

    /// <summary>Venue.Capacity (more than 0).</summary>
    [BindProperty]
    [Required(ErrorMessage = "Capacity is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be at least 1.")]
    public int? Capacity { get; set; }

    /// <summary>On the wireframe but not in the database yet.</summary>
    [BindProperty]
    public string? Description { get; set; }

    /// <summary>Shows the blank form. Nothing to load.</summary>
    public void OnGet()
    {
    }

    /// <summary>
    /// Save: redisplays the form with messages if the input is invalid or the API call fails,
    /// and goes to the Events page when the venue was added.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Stop here and redisplay the form with the validation messages above.
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // The Required rules guarantee these have values, so ! and .Value are safe.
        var request = new CreateVenueRequest(
            Name!,
            AddressLine1!,
            AddressLine2,
            City!,
            State!,
            ZipCode!,
            Capacity!.Value);

        try
        {
            using var response = await venuesApi.CreateAsync(request, cancellationToken);

            // The API rejected the values (for example, a name that's already used):
            // show its messages next to the matching fields.
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
            {
                await ModelState.AddApiValidationErrorsAsync(
                    response,
                    string.Empty,
                    "The venue could not be saved. Please check the values and try again.",
                    cancellationToken);

                return Page();
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to create venue. Status code: {StatusCode}", response.StatusCode);
                ModelState.AddModelError(string.Empty, "The venue could not be saved. Please try again.");
                return Page();
            }
        }
        // The API is down or unreachable.
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The venue API could not be reached");
            ModelState.AddModelError(string.Empty, "The venue API could not be reached. Please try again later.");
            return Page();
        }
        // The API took too long. (A visitor closing the page is not treated as an error.)
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The venue API request timed out");
            ModelState.AddModelError(string.Empty, "The venue API request timed out. Please try again later.");
            return Page();
        }

        // TempData lives for one more page load, so the Events page can show this message.
        TempData["VenueAdded"] = $"Venue added: {Name!.Trim()}";
        return RedirectToPage("/Events/Index");
    }
}
