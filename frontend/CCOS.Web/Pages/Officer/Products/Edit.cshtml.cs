using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Officer.Products;

/// <summary>
/// Stub for the Edit Product page (/Officer/Products/Edit/{id}). Content is added in a later story.
/// Officer-only: lock this down with [Authorize] once login is wired up.
/// </summary>
public class EditModel : PageModel
{
    /// <summary>
    /// The product id from the URL, or null when the URL has no id.
    /// </summary>
    public int? Id { get; private set; }

    public void OnGet(int? id)
    {
        Id = id;
    }
}
