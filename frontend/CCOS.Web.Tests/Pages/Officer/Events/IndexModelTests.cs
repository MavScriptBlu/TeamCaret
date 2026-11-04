using System.Net;
using System.Net.Http.Json;
using CCOS.Shared;
using CCOS.Web.Pages.Officer.Events;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Contrib.HttpClient;

namespace CCOS.Web.Tests.Pages.Officer.Events;

/// <summary>
/// Tests for the officer event list: the filter, Remove and Restore (CAR-56).
/// </summary>
public class IndexModelTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly IndexModel _model;

    public IndexModelTests()
    {
        var httpClient = _handlerMock.CreateClient();
        httpClient.BaseAddress = new Uri("http://localhost");

        _model = new IndexModel(new EventsApiClient(httpClient), new Mock<ILogger<IndexModel>>().Object)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
        };
    }

    private static EventDto Event(int id, bool isActive, bool withTicket = true) => new(
        id,
        $"Event {id}",
        null,
        new DateTime(2027, 3, 5, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2027, 3, 5, 2, 0, 0, DateTimeKind.Utc),
        40,
        false,
        isActive,
        1,
        "Main Hall",
        withTicket ? [new TicketTypeDto(id * 10, "General", 10m, 5m, true)] : []);

    private void ListReturns(params EventDto[] events) =>
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/events/officer")
            .ReturnsJsonResponse(events.ToList());

    [Fact]
    public async Task OnGet_shows_every_event_by_default()
    {
        ListReturns(Event(1, true), Event(2, false));

        await _model.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, _model.Events.Count);
        Assert.Equal(2, _model.TotalCount);
    }

    [Theory]
    [InlineData("active", 1)]
    [InlineData("inactive", 1)]
    [InlineData(null, 2)]
    public async Task OnGet_applies_the_Show_filter(string? show, int expected)
    {
        ListReturns(Event(1, true), Event(2, false));
        _model.Show = show;

        await _model.OnGetAsync(CancellationToken.None);

        Assert.Equal(expected, _model.Events.Count);
        Assert.Equal(2, _model.TotalCount);
    }

    [Fact]
    public async Task OnGet_sets_LoadFailed_when_the_API_is_down()
    {
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/events/officer")
            .Throws(new HttpRequestException("down"));

        await _model.OnGetAsync(CancellationToken.None);

        Assert.True(_model.LoadFailed);
    }

    [Fact]
    public async Task OnPostRemove_sends_a_DELETE_and_shows_the_removed_events()
    {
        _handlerMock.SetupRequest(HttpMethod.Delete, "http://localhost/api/events/3")
            .ReturnsResponse(HttpStatusCode.NoContent);

        var result = await _model.OnPostRemoveAsync(3, "active", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Officer/Events/Index", redirect.PageName);
        Assert.Equal("inactive", redirect.RouteValues!["show"]);
        Assert.NotNull(_model.TempData["EventRemoved"]);
    }

    [Fact]
    public async Task OnPostRemove_reports_an_unknown_event()
    {
        _handlerMock.SetupRequest(HttpMethod.Delete, "http://localhost/api/events/3")
            .ReturnsResponse(HttpStatusCode.NotFound);

        await _model.OnPostRemoveAsync(3, null, CancellationToken.None);

        Assert.NotNull(_model.TempData["EventError"]);
        Assert.Null(_model.TempData["EventRemoved"]);
    }

    [Fact]
    public async Task OnPostRemove_rejects_a_bad_id_without_calling_the_API()
    {
        await _model.OnPostRemoveAsync(0, null, CancellationToken.None);

        Assert.NotNull(_model.TempData["EventError"]);
        _handlerMock.VerifyAnyRequest(Times.Never());
    }

    [Fact]
    public async Task OnPostRestore_saves_the_event_again_with_IsActive_true()
    {
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/events/officer/2")
            .ReturnsJsonResponse(Event(2, false));
        UpdateEventRequest? sent = null;
        _handlerMock.SetupRequest(HttpMethod.Put, "http://localhost/api/events/2", async request =>
            {
                sent = await request.Content!.ReadFromJsonAsync<UpdateEventRequest>();
                return true;
            })
            .ReturnsJsonResponse(Event(2, true));

        var result = await _model.OnPostRestoreAsync(2, "inactive", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("active", redirect.RouteValues!["show"]);
        Assert.NotNull(sent);
        Assert.True(sent.IsActive);
        Assert.Equal(10m, sent.TicketPrice);
        Assert.Equal("Event restored: Event 2", _model.TempData["EventRestored"]);
    }

    [Fact]
    public async Task OnPostRestore_sends_events_with_no_ticket_to_the_Edit_page_message()
    {
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/events/officer/2")
            .ReturnsJsonResponse(Event(2, false, withTicket: false));

        await _model.OnPostRestoreAsync(2, null, CancellationToken.None);

        Assert.Contains("Open Edit", (string)_model.TempData["EventError"]!);
        _handlerMock.VerifyRequest(HttpMethod.Put, "http://localhost/api/events/2", Times.Never());
    }
}
