using System.Net;
using System.Text.Json;
using CCOS.Web.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CCOS.Web.Tests.ApiDown;

/// <summary>
/// "The API is down" feedback (CAR-60): the safety-net filter, and the friendly status page the
/// real site shows when the API can't be reached.
/// </summary>
public class ApiUnavailableTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // The filter on its own
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(ApiProblems))]
    public async Task Filter_ApiProblem_Becomes503(Exception problem)
    {
        var (executing, executed) = CreateContexts(problem);

        await new ApiUnavailablePageFilter(NullLogger<ApiUnavailablePageFilter>.Instance)
            .OnPageHandlerExecutionAsync(executing, () => Task.FromResult(executed));

        Assert.True(executed.ExceptionHandled);
        var result = Assert.IsType<StatusCodeResult>(executed.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
    }

    public static TheoryData<Exception> ApiProblems() => new()
    {
        new HttpRequestException("Connection refused"),
        new TaskCanceledException("Timed out"),
        new JsonException("Not JSON"),
    };

    [Fact]
    public async Task Filter_OtherErrors_AreLeftAlone()
    {
        var (executing, executed) = CreateContexts(new InvalidOperationException("A real bug"));

        await new ApiUnavailablePageFilter(NullLogger<ApiUnavailablePageFilter>.Instance)
            .OnPageHandlerExecutionAsync(executing, () => Task.FromResult(executed));

        Assert.False(executed.ExceptionHandled);
        Assert.Null(executed.Result);
    }

    [Fact]
    public void VisitorClosingThePage_IsNotAnApiProblem()
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();

        Assert.False(ApiUnavailablePageFilter.IsApiUnavailable(new TaskCanceledException(), aborted.Token));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // The real site with the API switched off
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProductPage_ApiDown_ShowsTheFriendlyMessage()
    {
        using var site = CreateSiteWithApiDown();
        using var client = site.CreateClient();

        var response = await client.GetAsync("/Products/Details/1");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        // Text from the page model is HTML-encoded, so match words without apostrophes.
        Assert.Contains("reachable right now", body);
        Assert.Contains("Nothing was changed", body);
    }

    [Fact]
    public async Task UnknownPage_ShowsNotFoundMessage()
    {
        using var site = CreateSiteWithApiDown();
        using var client = site.CreateClient();

        var response = await client.GetAsync("/no-such-page");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("find that page", body);
    }

    [Fact]
    public async Task MerchList_ApiDown_StillShowsItsOwnMessage()
    {
        using var site = CreateSiteWithApiDown();
        using var client = site.CreateClient();

        var response = await client.GetAsync("/Products");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("be loaded right now", body);
    }

    /// <summary>
    /// The real website, pointed at an address where nothing answers (port 9 on this machine),
    /// so every API call fails the same way it does when CCOS.Api isn't running.
    /// </summary>
    private static WebApplicationFactory<Program> CreateSiteWithApiDown() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("Api:BaseUrl", "http://127.0.0.1:9/"));

    private static (PageHandlerExecutingContext Executing, PageHandlerExecutedContext Executed) CreateContexts(Exception problem)
    {
        var pageContext = new PageContext(new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new CompiledPageActionDescriptor()));
        var handlerInstance = new object();

        var executing = new PageHandlerExecutingContext(pageContext, [], null, new Dictionary<string, object?>(), handlerInstance);
        var executed = new PageHandlerExecutedContext(pageContext, [], null, handlerInstance) { Exception = problem };
        return (executing, executed);
    }
}
