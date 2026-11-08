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
public sealed class ApiUnavailablePageFilter(ILogger<ApiUnavailablePageFilter> logger) : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var executed = await next();

        if (executed.Exception is { } ex
            && !executed.ExceptionHandled
            && IsApiUnavailable(ex, context.HttpContext.RequestAborted))
        {
            logger.LogError(ex, "CCOS.Api couldn't be reached while loading {Page}.", context.ActionDescriptor.DisplayName);
            executed.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            executed.ExceptionHandled = true;
        }
    }

    /// <summary>
    /// True when the exception means the API is down, too slow, or sent something unreadable.
    /// </summary>
    public static bool IsApiUnavailable(Exception ex, CancellationToken requestAborted) =>
        ex is HttpRequestException or JsonException
        || (ex is TaskCanceledException && !requestAborted.IsCancellationRequested);
}
