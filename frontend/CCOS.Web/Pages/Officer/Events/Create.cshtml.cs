using Microsoft.AspNetCore.Mvc;

namespace CCOS.Web.Pages.Officer.Events;

/// <summary>
/// Create Event page (/Officer/Events/Create). Save and Cancel both go back to /Events.
/// Nothing is saved or validated yet.
/// Officer-only: lock this down with [Authorize] once login is wired up.
/// </summary>
public class CreateModel : EventFormPageModel
{
    public override string Heading => "Create Event";
    public override string ImagePrompt => "Drag and drop an image, or click to choose one";
    public override string SaveText => "Save";
    public override string CancelPage => "/Events/Index";

    public void OnGet()
    {
    }

    /// <summary>
    /// Save: nothing is saved yet, so this just goes back to the Events page.
    /// </summary>
    public IActionResult OnPost() => RedirectToPage("/Events/Index");
}
