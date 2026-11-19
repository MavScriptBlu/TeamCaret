using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Account;

/// <summary>
/// "Officers only" (/Account/AccessDenied, CAR-69). A signed-in person who isn't an officer lands
/// here after opening an officer page. Visitors who aren't signed in go to Log in instead.
/// </summary>
public class AccessDeniedModel : PageModel
{
    /// <summary>The page they tried to open (shown nowhere; kept so a link back could use it later).</summary>
    [BindProperty(SupportsGet = true)]
    [SuppressMessage("Major Code Smell", "S3996:URI properties should not be strings", Justification = "ReturnUrl is a relative path from the query string; LocalRedirect and Url.IsLocalUrl take strings. Open redirects are blocked by AccountSession.SafeReturnUrl.")]
    public string? ReturnUrl { get; set; }

    public void OnGet()
    {
    }
}
