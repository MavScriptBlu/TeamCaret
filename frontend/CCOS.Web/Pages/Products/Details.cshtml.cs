using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Products;

/// <summary>
/// Stub for the Product Details page (/Products/Details/{id}). Content is added in a later story.
/// </summary>
public class DetailsModel : PageModel
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
