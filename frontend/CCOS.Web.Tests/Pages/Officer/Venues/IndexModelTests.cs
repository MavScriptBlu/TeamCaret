using System.Net;
using System.Net.Http.Json;
using CCOS.Shared;
using CCOS.Web.Pages.Officer.Venues;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Contrib.HttpClient;

namespace CCOS.Web.Tests.Pages.Officer.Venues;

/// <summary>
/// Tests for the officer venue list: the filter, Remove and Restore (CAR-57, CAR-58).
/// </summary>
public class IndexModelTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly IndexModel _model;

    public IndexModelTests()
    {
        var httpClient = _handlerMock.CreateClient();
        httpClient.BaseAddress = new Uri("http://localhost");

        _model = new IndexModel(new VenuesApiClient(httpClient), new Mock<ILogger<IndexModel>>().Object)
        {
            // A real TempData so the tests can read the messages the page sets.
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
        };
    }

    private static VenueDetailDto Venue(int id, string name, bool isActive = true, string? addressLine1 = "1 Main St") =>
        new(id, name, 50, isActive, addressLine1, null, addressLine1 is null ? null : "Plover", addressLine1 is null ? null : "WI", addressLine1 is null ? null : "54467");

    // ---- list and filter ----

    [Theory]
    [InlineData(null, 3)]
    [InlineData("active", 2)]
    [InlineData("inactive", 1)]
    public async Task OnGet_filters_by_Show(string? show, int expectedCount)
    {
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/venues/officer")
            .ReturnsJsonResponse(new List<VenueDetailDto>
            {
                Venue(1, "A"),
                Venue(2, "B"),
                Venue(3, "C", isActive: false),
            });
        _model.Show = show;

        await _model.OnGetAsync(CancellationToken.None);

        Assert.Equal(3, _model.TotalCount);
        Assert.Equal(expectedCount, _model.Venues.Count);
        Assert.False(_model.LoadFailed);
    }

    [Fact]
    public async Task OnGet_sets_LoadFailed_when_the_api_is_down()
    {
        _handlerMock.SetupAnyRequest().ThrowsAsync(new HttpRequestException("down"));

        await _model.OnGetAsync(CancellationToken.None);

        Assert.True(_model.LoadFailed);
    }

    // ---- Remove ----

    [Fact]
    public async Task Remove_calls_DELETE_and_says_it_can_be_restored()
    {
        _handlerMock.SetupRequest(HttpMethod.Delete, "http://localhost/api/venues/7")
            .ReturnsResponse(HttpStatusCode.NoContent);

        var result = await _model.OnPostRemoveAsync(7, "active", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        // The removed venue no longer shows in the "In the venue list" view, so go to the removed ones.
        Assert.Equal("inactive", redirect.RouteValues!["show"]);
        Assert.Contains("restore", (string)_model.TempData["VenueRemoved"]!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Remove_shows_an_error_when_the_venue_is_not_found()
    {
        _handlerMock.SetupRequest(HttpMethod.Delete, "http://localhost/api/venues/7")
            .ReturnsResponse(HttpStatusCode.NotFound);

        await _model.OnPostRemoveAsync(7, null, CancellationToken.None);

        Assert.Contains("could not be found", (string)_model.TempData["VenueError"]!);
        Assert.False(_model.TempData.ContainsKey("VenueRemoved"));
    }

    [Fact]
    public async Task Remove_shows_an_error_when_the_api_is_down()
    {
        _handlerMock.SetupAnyRequest().ThrowsAsync(new HttpRequestException("down"));

        await _model.OnPostRemoveAsync(7, null, CancellationToken.None);

        Assert.Contains("could not be reached", (string)_model.TempData["VenueError"]!);
    }

    [Fact]
    public async Task Remove_rejects_a_bad_id_without_calling_the_api()
    {
        await _model.OnPostRemoveAsync(0, null, CancellationToken.None);

        Assert.True(_model.TempData.ContainsKey("VenueError"));
        _handlerMock.VerifyAnyRequest(Times.Never());
    }

    // ---- Restore ----

    [Fact]
    public async Task Restore_saves_the_venue_again_with_IsActive_true()
    {
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/venues/7")
            .ReturnsJsonResponse(Venue(7, "Old Gym", isActive: false));
        UpdateVenueRequest? sent = null;
        _handlerMock.SetupRequest(HttpMethod.Put, "http://localhost/api/venues/7", async request =>
            {
                sent = await request.Content!.ReadFromJsonAsync<UpdateVenueRequest>();
                return true;
            })
            .ReturnsJsonResponse(Venue(7, "Old Gym"));

        var result = await _model.OnPostRestoreAsync(7, "inactive", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("active", redirect.RouteValues!["show"]);
        Assert.NotNull(sent);
        Assert.True(sent.IsActive);
        Assert.Equal("Old Gym", sent.Name);
        Assert.Equal("54467", sent.ZipCode);
        Assert.Equal("Venue restored: Old Gym", _model.TempData["VenueRestored"]);
    }

    [Fact]
    public async Task Restore_asks_for_an_address_first_when_the_venue_has_none()
    {
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/venues/7")
            .ReturnsJsonResponse(Venue(7, "Sample Room", isActive: false, addressLine1: null));

        await _model.OnPostRestoreAsync(7, null, CancellationToken.None);

        Assert.Contains("no address yet", (string)_model.TempData["VenueError"]!);
        _handlerMock.VerifyRequest(HttpMethod.Put, "http://localhost/api/venues/7", Times.Never());
    }

    [Fact]
    public async Task Restore_shows_an_error_when_the_venue_is_not_found()
    {
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/venues/7")
            .ReturnsResponse(HttpStatusCode.NotFound);

        await _model.OnPostRestoreAsync(7, null, CancellationToken.None);

        Assert.Contains("could not be found", (string)_model.TempData["VenueError"]!);
    }
}
