using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Events;

/// <summary>
/// Stub for the Event Details page (/Events/Details/{id}). Content is added in a later story.
/// </summary>
public class DetailsModel : PageModel
{
    /// <summary>
    /// The event id from the URL, or null when the URL has no id.
    /// </summary>
    public int? Id { get; private set; }

    public void OnGet(int? id)
    {
        Id = id;
    }
}
