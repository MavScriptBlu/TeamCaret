using System.Net;
using System.Text.Json;
using CCOS.Web.Filters;
using Microsoft.AspNetCore.Hosting;
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
/// The real website, pointed at an address where nothing answers (port 9 on this machine), so
/// every API call fails the same way it does when CCOS.Api isn't running. Shared by all the tests
/// in ApiUnavailableTests, so the site starts once.
/// </summary>
public sealed class ApiDownSite : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Api:BaseUrl", "http://127.0.0.1:9/");
    }
}

/// <summary>
/// "The API is down" feedback (CAR-60): the safety-net filter, and the friendly status page the
/// real site shows when the API can't be reached.
/// </summary>
public class ApiUnavailableTests(ApiDownSite site) : IClassFixture<ApiDownSite>
{
    // ─────────────────────────────────────────────────────────────────────────
    // The filter on its own
    // ─────────────────────────────────────────────────────────────────────────

    public static TheoryData<Exception> ApiProblems => new()
    {
        new HttpRequestException("Connection refused"),
        new TaskCanceledException("Timed out"),
        new JsonException("Not JSON"),
    };

    [Theory]
    [MemberData(nameof(ApiProblems))]
    public async Task Filter_ApiProblem_Becomes503(Exception problem)
    {
        var executed = await RunFilterAsync(problem);

        Assert.True(executed.ExceptionHandled);
        var result = Assert.IsType<StatusCodeResult>(executed.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
    }

    [Fact]
    public async Task Filter_OtherErrors_AreLeftAlone()
    {
        var executed = await RunFilterAsync(new InvalidOperationException("A real bug"));

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

    // Text from the page model is HTML-encoded, so these match words without apostrophes.
    [Theory]
    [InlineData("/Products/Details/1", HttpStatusCode.ServiceUnavailable, "reachable right now")]
    [InlineData("/no-such-page", HttpStatusCode.NotFound, "find that page")]
    [InlineData("/Products", HttpStatusCode.OK, "be loaded right now")] // the merch page's own message
    public async Task Pages_ApiDown_ShowAFriendlyMessage(string path, HttpStatusCode expectedStatus, string expectedText)
    {
        using var client = site.CreateClient();

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Contains(expectedText, body);
    }

    /// <summary>Runs the filter once, as if the page handler had thrown the given exception.</summary>
    private static async Task<PageHandlerExecutedContext> RunFilterAsync(Exception problem)
    {
        var pageContext = new PageContext(new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new CompiledPageActionDescriptor()));
        var handlerInstance = new object();

        var executing = new PageHandlerExecutingContext(pageContext, [], null, new Dictionary<string, object?>(), handlerInstance);
        var executed = new PageHandlerExecutedContext(pageContext, [], null, handlerInstance) { Exception = problem };

        await new ApiUnavailablePageFilter(NullLogger<ApiUnavailablePageFilter>.Instance)
            .OnPageHandlerExecutionAsync(executing, () => Task.FromResult(executed));

        return executed;
    }
}
