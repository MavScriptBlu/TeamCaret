using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Web.Pages;

/// <summary>
/// Stub for the Home page (/). Content is added in a later story.
/// </summary>
public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        return RedirectToPage("/Products/Index");
    }
}
