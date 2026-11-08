using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages;

/// <summary>
/// Friendly page for errors that come back with no page of their own (CAR-60), shown through
/// UseStatusCodePagesWithReExecute in Program.cs: "not found", "the club's system isn't reachable",
/// and anything else. The original status code is kept, so browsers and tests still see it.
/// </summary>
public class StatusModel : PageModel
{
    /// <summary>The status code from the address (/Status/503).</summary>
    public int Code { get; private set; }

    public string Heading { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public void OnGet(int code)
    {
        Code = code;
        (Heading, Message) = code switch
        {
            StatusCodes.Status404NotFound => (
                "We couldn't find that page",
                "It may have been removed, or the address may be wrong."),
            StatusCodes.Status503ServiceUnavailable => (
                "The club's system isn't reachable right now",
                "Nothing was changed. Please try again in a few minutes."),
            _ => (
                "Something went wrong",
                "Please try again in a few minutes."),
        };
    }
}
