using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CCOS.Web.Filters;

/// <summary>
/// Safety net for every page (CAR-60). Most pages catch "the API can't be reached" themselves and
/// show their own message. If one doesn't, this turns the failure into a 503 instead of a crash,
/// and the status page (Pages/Status.cshtml) tells the person the club's system isn't reachable.
/// A visitor closing the page (cancellation) is not treated as an API problem.
/// </summary>
public sealed partial class ApiUnavailablePageFilter(ILogger<ApiUnavailablePageFilter> logger) : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var executed = await next();

        if (executed.Exception is not { } ex
            || executed.ExceptionHandled
            || !IsApiUnavailable(ex, context.HttpContext.RequestAborted))
        {
            return;
        }

        LogApiUnavailable(logger, ex, context.ActionDescriptor.DisplayName ?? "an unknown page");
        executed.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
        executed.ExceptionHandled = true;
    }

    /// <summary>
    /// True when the exception means the API is down, too slow, or sent something unreadable.
    /// A cancellation only counts when the visitor didn't close the page themselves (a timeout).
    /// </summary>
    public static bool IsApiUnavailable(Exception ex, CancellationToken requestAborted) => ex switch
    {
        HttpRequestException or JsonException => true,
        TaskCanceledException => !requestAborted.IsCancellationRequested,
        _ => false,
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "CCOS.Api couldn't be reached while loading {Page}.")]
    private static partial void LogApiUnavailable(ILogger logger, Exception exception, string page);
}
