using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages;

/// <summary>
/// Privacy page (/Privacy). Just shows the page text from Privacy.cshtml; there is no logic to run.
/// </summary>
public class PrivacyModel : PageModel
{
    /// <summary>Runs when the page is opened. Nothing to load.</summary>
    public void OnGet()
    {
    }
}

