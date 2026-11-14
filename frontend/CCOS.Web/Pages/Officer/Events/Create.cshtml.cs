using System.Net;
using CCOS.Shared;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Web.Pages.Officer.Events;

/// <summary>
/// Create Event page (/Officer/Events/Create). Checks what the officer typed, sends the new event
/// to CCOS.Api (CAR-55), then goes to /Events with an "Event added" message. Cancel goes back to /Events.
/// The fields and their checks are in EventFormPageModel, shared with the Edit page.
/// Officer-only: locked by AuthorizeFolder("/Officer") in Program.cs (CAR-69).
/// </summary>
public class CreateModel(EventsApiClient eventsApi, VenuesApiClient venuesApi, ILogger<CreateModel> logger)
    : EventFormPageModel
{
    public override string Heading => "Create Event";
    public override string ImagePrompt => "Drag and drop an image, or click to choose one";
    public override string SaveText => "Save";
    public override string CancelPage => "/Events/Index";

    /// <summary>
    /// Shows the blank form with the real venues in the dropdown.
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadVenuesAsync(venuesApi, logger, cancellationToken);
    }

    /// <summary>
    /// Save: redisplays the form with messages if the input is invalid or the API call fails,
    /// and goes to the Events page when the event was added.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Single-field rules first, then the ones that compare fields (end after start, prices).
        var times = ModelState.IsValid ? CheckAcrossFields() : null;

        if (times is null)
        {
            await LoadVenuesAsync(venuesApi, logger, cancellationToken);
            return Page();
        }

        // The Required rules guarantee these have values, so ! and .Value are safe.
        var request = new CreateEventRequest(
            Name!,
            Description,
            times.Value.StartsAtUtc,
            times.Value.EndsAtUtc,
            VenueId!.Value,
            TicketCapacity!.Value,
            MembersOnly,
            TicketPrice!.Value,
            MemberPrice!.Value);

        try
        {
            using var response = await eventsApi.CreateAsync(request, cancellationToken);

            // The API rejected the values: show its messages next to the matching fields.
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
            {
                await ShowApiErrorsAsync(response, cancellationToken);
                await LoadVenuesAsync(venuesApi, logger, cancellationToken);
                return Page();
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to create event. Status code: {StatusCode}", response.StatusCode);
                ModelState.AddModelError(string.Empty, "The event could not be saved. Please try again.");
                await LoadVenuesAsync(venuesApi, logger, cancellationToken);
                return Page();
            }
        }
        // The API is down or unreachable.
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The event API could not be reached");
            ModelState.AddModelError(string.Empty, "The event API could not be reached. Please try again later.");
            await LoadVenuesAsync(venuesApi, logger, cancellationToken);
            return Page();
        }
        // The API took too long. (A visitor closing the page is not treated as an error.)
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The event API request timed out");
            ModelState.AddModelError(string.Empty, "The event API request timed out. Please try again later.");
            await LoadVenuesAsync(venuesApi, logger, cancellationToken);
            return Page();
        }

        // TempData lives for one more page load, so the Events page can show this message.
        TempData["EventAdded"] = $"Event added: {Name!.Trim()}";
        return RedirectToPage("/Events/Index");
    }
}
