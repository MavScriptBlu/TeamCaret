using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages;

/// <summary>
/// Friendly page for errors that come back with no page of their own (CAR-60), shown through
/// UseStatusCodePagesWithReExecute in Program.cs: "not found", "the club's system isn't reachable",
/// and anything else. The original status code is kept, so browsers and tests still see it.
/// Never cached, so a 503 page doesn't stick around after the API is back.
/// </summary>
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public partial class StatusModel(ILogger<StatusModel> logger) : PageModel
{
    /// <summary>The status code from the address (/Status/503).</summary>
    public int Code { get; private set; }

    /// <summary>The page heading for this status code.</summary>
    public string Heading => Code switch
    {
        StatusCodes.Status404NotFound => "We couldn't find that page",
        StatusCodes.Status503ServiceUnavailable => "The club's system isn't reachable right now",
        _ => "Something went wrong",
    };

    /// <summary>The line under the heading for this status code.</summary>
    public string Message => Code switch
    {
        StatusCodes.Status404NotFound => "It may have been removed, or the address may be wrong.",
        StatusCodes.Status503ServiceUnavailable => "Nothing was changed. Please try again in a few minutes.",
        _ => "Please try again in a few minutes.",
    };

    public void OnGet(int code)
    {
        Code = code;

        // 404 and 503 have their own wording; log anything else so it can be looked into.
        if (code is not (StatusCodes.Status404NotFound or StatusCodes.Status503ServiceUnavailable))
        {
            LogUnexpectedStatus(logger, code);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Showed the general error page for status code {StatusCode}.")]
    private static partial void LogUnexpectedStatus(ILogger logger, int statusCode);
}
