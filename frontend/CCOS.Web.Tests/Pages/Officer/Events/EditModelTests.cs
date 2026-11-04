using System.Net;
using System.Net.Http.Json;
using CCOS.Shared;
using CCOS.Web.Pages.Officer.Events;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Contrib.HttpClient;

namespace CCOS.Web.Tests.Pages.Officer.Events;

/// <summary>
/// Tests for the Edit Event page: loading, saving through PUT, and Remove/Restore with the
/// "Show in the event list" box (CAR-55, CAR-56).
/// </summary>
public class EditModelTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly EditModel _model;

    public EditModelTests()
    {
        var httpClient = _handlerMock.CreateClient();
        httpClient.BaseAddress = new Uri("http://localhost");

        _model = new EditModel(
            new EventsApiClient(httpClient),
            new VenuesApiClient(httpClient),
            new Mock<ILogger<EditModel>>().Object)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
        };

        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/venues")
            .ReturnsJsonResponse(new List<VenueDto> { new(1, "Main Hall") });
    }

    private static EventDto Event(bool isActive = true, int venueId = 1, string venueName = "Main Hall") => new(
        7,
        "Robot Night",
        "Bring a robot.",
        new DateTime(2027, 3, 5, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2027, 3, 5, 2, 0, 0, DateTimeKind.Utc),
        40,
        true,
        isActive,
        venueId,
        venueName,
        [new TicketTypeDto(70, "General", 10m, 5m, true)]);

    private void FillForm(bool isActive = true)
    {
        _model.Name = "Robot Night";
        _model.Date = new DateOnly(2027, 3, 4);
        _model.StartTime = new TimeOnly(18, 0);
        _model.EndTime = new TimeOnly(20, 0);
        _model.VenueId = 1;
        _model.TicketCapacity = 40;
        _model.TicketPrice = 10m;
        _model.MemberPrice = 5m;
        _model.IsActive = isActive;
    }

    [Fact]
    public async Task OnGet_fills_the_form_in_club_time_from_the_officer_endpoint()
    {
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/events/officer/7")
            .ReturnsJsonResponse(Event(isActive: false));

        await _model.OnGetAsync(7, CancellationToken.None);

        Assert.True(_model.Found);
        Assert.Equal("Robot Night", _model.Name);
        Assert.Equal(new DateOnly(2027, 3, 4), _model.Date);
        Assert.Equal(new TimeOnly(18, 0), _model.StartTime);
        Assert.Equal(10m, _model.TicketPrice);
        Assert.False(_model.IsActive);
        Assert.True(_model.ShowActiveBox);
    }

    [Fact]
    public async Task OnGet_keeps_the_events_venue_in_the_dropdown_even_if_the_venue_was_removed()
    {
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/events/officer/7")
            .ReturnsJsonResponse(Event(venueId: 9, venueName: "Old Gym"));

        await _model.OnGetAsync(7, CancellationToken.None);

        Assert.Contains(_model.VenueOptions, option => option.Value == "9" && option.Text == "Old Gym");
    }

    [Fact]
    public async Task OnGet_sends_404_for_an_unknown_event()
    {
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/events/officer/7")
            .ReturnsResponse(HttpStatusCode.NotFound);

        await _model.OnGetAsync(7, CancellationToken.None);

        Assert.False(_model.Found);
        Assert.Equal(StatusCodes.Status404NotFound, _model.Response.StatusCode);
    }

    [Fact]
    public async Task OnGet_sets_LoadFailed_when_the_API_is_down()
    {
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/events/officer/7")
            .Throws(new HttpRequestException("down"));

        await _model.OnGetAsync(7, CancellationToken.None);

        Assert.True(_model.LoadFailed);
    }

    [Fact]
    public async Task OnPost_sends_a_PUT_and_goes_back_to_the_event_page()
    {
        FillForm();
        UpdateEventRequest? sent = null;
        _handlerMock.SetupRequest(HttpMethod.Put, "http://localhost/api/events/7", async request =>
            {
                sent = await request.Content!.ReadFromJsonAsync<UpdateEventRequest>();
                return true;
            })
            .ReturnsJsonResponse(Event());

        var result = await _model.OnPostAsync(7, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Events/Details", redirect.PageName);
        Assert.Equal(7, redirect.RouteValues!["id"]);
        Assert.NotNull(sent);
        Assert.True(sent.IsActive);
        Assert.Equal("Event saved: Robot Night", _model.TempData["EventSaved"]);
    }

    [Fact]
    public async Task OnPost_with_the_box_unticked_removes_the_event_and_goes_to_the_officer_list()
    {
        FillForm(isActive: false);
        UpdateEventRequest? sent = null;
        _handlerMock.SetupRequest(HttpMethod.Put, "http://localhost/api/events/7", async request =>
            {
                sent = await request.Content!.ReadFromJsonAsync<UpdateEventRequest>();
                return true;
            })
            .ReturnsJsonResponse(Event(isActive: false));

        var result = await _model.OnPostAsync(7, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Officer/Events/Index", redirect.PageName);
        Assert.NotNull(sent);
        Assert.False(sent.IsActive);
        Assert.Contains("removed", (string)_model.TempData["EventRemoved"]!);
    }

    [Fact]
    public async Task OnPost_shows_the_API_message_when_the_capacity_is_below_the_tickets_sold()
    {
        FillForm();
        _handlerMock.SetupRequest(HttpMethod.Put, "http://localhost/api/events/7")
            .ReturnsJsonResponse(
                HttpStatusCode.BadRequest,
                new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["TicketCapacity"] = ["12 tickets are already sold, so the capacity can't be lower than that."],
                }));

        var result = await _model.OnPostAsync(7, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        var errors = _model.ModelState[nameof(_model.TicketCapacity)]!.Errors;
        Assert.Contains("12 tickets", errors[0].ErrorMessage);
        Assert.True(_model.Found);
    }

    [Fact]
    public async Task OnPost_tells_the_officer_when_someone_else_saved_first()
    {
        FillForm();
        _handlerMock.SetupRequest(HttpMethod.Put, "http://localhost/api/events/7")
            .ReturnsResponse(HttpStatusCode.Conflict);

        var result = await _model.OnPostAsync(7, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Contains("Someone else", _model.ModelState[string.Empty]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task OnPost_sends_404_when_the_event_is_gone()
    {
        FillForm();
        _handlerMock.SetupRequest(HttpMethod.Put, "http://localhost/api/events/7")
            .ReturnsResponse(HttpStatusCode.NotFound);

        await _model.OnPostAsync(7, CancellationToken.None);

        Assert.False(_model.Found);
        Assert.Equal(StatusCodes.Status404NotFound, _model.Response.StatusCode);
    }

    [Fact]
    public async Task OnPost_does_not_call_the_API_when_the_end_is_before_the_start()
    {
        FillForm();
        _model.EndTime = new TimeOnly(17, 0);

        var result = await _model.OnPostAsync(7, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        _handlerMock.VerifyRequest(HttpMethod.Put, "http://localhost/api/events/7", Times.Never());
    }
}
