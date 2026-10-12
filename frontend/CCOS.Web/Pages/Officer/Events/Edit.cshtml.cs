using CCOS.Web.Pages.Events;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Web.Pages.Officer.Events;

/// <summary>
/// Edit Event page (/Officer/Events/Edit/{id}). Save changes and Cancel both go back to
/// the event page, /Events/Details/{id}. Nothing is saved or validated yet.
/// Officer-only: lock this down with [Authorize] once login is wired up.
/// </summary>
public class EditModel : EventFormPageModel
{
    /// <summary>
    /// The event id from the URL, or null when the URL has no id.
    /// </summary>
    public int? Id { get; private set; }

    /// <summary>
    /// True when the id matches an event, so the form can be shown.
    /// </summary>
    public bool Found { get; private set; }

    public override string Heading => "Edit Event";
    public override string ImagePrompt => "Change image: drag and drop, or click to choose one";
    public override string SaveText => "Save changes";
    public override string CancelPage => "/Events/Details";
    public override int? CancelRouteId => Id;

    public void OnGet(int? id)
    {
        Id = id;

        // Officers can edit any event, including canceled ones.
        var ev = id is int eventId ? SampleEvents.Find(eventId) : null;
        if (ev is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        Found = true;
        Fill(ev);
    }

    /// <summary>
    /// Save changes: nothing is saved yet, so this just goes back to the event page.
    /// </summary>
    public IActionResult OnPost(int? id) => RedirectToPage("/Events/Details", new { id });
}
