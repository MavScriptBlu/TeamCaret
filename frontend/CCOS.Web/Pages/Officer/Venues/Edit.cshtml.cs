using System.Net;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Extensions;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Web.Pages.Officer.Venues;

/// <summary>
/// Edit Venue page (/Officer/Venues/Edit/{id}). Loads the venue from CCOS.Api, checks what the
/// officer typed, sends the changes to PUT /api/venues/{id} (CAR-57), then goes back to the venue
/// list with a "Venue saved" message. The "Show in the venue list" box removes (unticked) or
/// restores (ticked) the venue (CAR-58).
/// Officer-only: lock this down with [Authorize] once login is wired up.
/// </summary>
public class EditModel(VenuesApiClient venuesApi, ILogger<EditModel> logger) : VenueFormPageModel
{
    /// <summary>
    /// The venue id from the URL, or null when the URL has no id.
    /// </summary>
    public int? Id { get; private set; }

    /// <summary>
    /// True when the id matches a venue, so the form can be shown.
    /// </summary>
    public bool Found { get; private set; }

    /// <summary>
    /// True when the API couldn't be reached, so the page shows an error message instead.
    /// </summary>
    public bool LoadFailed { get; private set; }

    public override string Heading => "Edit Venue";
    public override string ImagePrompt => "Change image: drag and drop, or click to choose one";
    public override string SaveText => "Save changes";
    public override string CancelPage => "/Officer/Venues/Index";
    public override bool ShowActiveBox => true;

    /// <summary>
    /// Loads the venue and fills the form with its current values. Sends a 404 (and leaves Found
    /// false, so no form shows) when the id is missing or unknown.
    /// </summary>
    /// <param name="id">The venue id from the URL.</param>
    public async Task OnGetAsync(int? id, CancellationToken cancellationToken)
    {
        Id = id;

        try
        {
            var venue = id is int venueId ? await venuesApi.GetAsync(venueId, cancellationToken) : null;
            if (venue is null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            Found = true;
            Name = venue.Name;
            AddressLine1 = venue.AddressLine1;
            AddressLine2 = venue.AddressLine2;
            City = venue.City;
            State = venue.State;
            ZipCode = venue.ZipCode;
            Capacity = venue.Capacity;
            IsActive = venue.IsActive;
        }
        // Same handling as the product pages: a timeout counts as a failure, but a visitor
        // closing the page (cancellation) is not an error worth logging.
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't load venue {VenueId} for editing from CCOS.Api.", id);
            LoadFailed = true;
        }
    }

    /// <summary>
    /// Save changes: redisplays the form with messages if the input is invalid or the API call
    /// fails, and goes back to the venue list when the venue was saved.
    /// </summary>
    /// <param name="id">The venue id from the URL.</param>
    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken cancellationToken)
    {
        Id = id;

        // The form is only on screen for a venue that exists, so keep showing it after an error.
        Found = true;

        if (id is not int venueId)
        {
            Found = false;
            Response.StatusCode = StatusCodes.Status404NotFound;
            return Page();
        }

        // Stop here and redisplay the form with the validation messages above.
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // The Required rules guarantee these have values, so ! and .Value are safe.
        var request = new UpdateVenueRequest(
            Name!,
            AddressLine1!,
            AddressLine2,
            City!,
            State!,
            ZipCode!,
            Capacity!.Value,
            IsActive);

        try
        {
            using var response = await venuesApi.UpdateAsync(venueId, request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Found = false;
                Response.StatusCode = StatusCodes.Status404NotFound;
                return Page();
            }

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
                logger.LogWarning(
                    "Failed to update venue {VenueId}. Status code: {StatusCode}", venueId, response.StatusCode);
                ModelState.AddModelError(string.Empty, "The venue could not be saved. Please try again.");
                return Page();
            }
        }
        // The API is down or unreachable.
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The venue API could not be reached while saving venue {VenueId}", venueId);
            ModelState.AddModelError(string.Empty, "The venue API could not be reached. Please try again later.");
            return Page();
        }
        // The API took too long. (A visitor closing the page is not treated as an error.)
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The venue API request timed out while saving venue {VenueId}", venueId);
            ModelState.AddModelError(string.Empty, "The venue API request timed out. Please try again later.");
            return Page();
        }

        // TempData lives for one more page load, so the venue list can show this message.
        TempData["VenueSaved"] = IsActive
            ? $"Venue saved: {Name!.Trim()}"
            : $"Venue removed: {Name!.Trim()}. You can restore it from the venue list.";
        return RedirectToPage("/Officer/Venues/Index");
    }
}
