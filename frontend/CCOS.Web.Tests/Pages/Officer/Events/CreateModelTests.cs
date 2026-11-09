using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using CCOS.Shared;
using CCOS.Web.Pages.Events;
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
/// Tests for the Create Event page: the form rules, club time to UTC, and saving through the API (CAR-55).
/// </summary>
public class CreateModelTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly CreateModel _model;

    public CreateModelTests()
    {
        var httpClient = _handlerMock.CreateClient();
        httpClient.BaseAddress = new Uri("http://localhost");

        _model = new CreateModel(
            new EventsApiClient(httpClient),
            new VenuesApiClient(httpClient),
            new Mock<ILogger<CreateModel>>().Object)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
        };

        // The venue dropdown reloads whenever the form is shown again.
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/venues")
            .ReturnsJsonResponse(new List<VenueDto> { new(1, "Main Hall") });
    }

    // Fills the form the way an officer would, so each test only changes what it checks.
    private void FillForm()
    {
        _model.Name = "Robot Night";
        _model.Date = new DateOnly(2027, 3, 4);
        _model.StartTime = new TimeOnly(18, 0);
        _model.EndTime = new TimeOnly(20, 0);
        _model.VenueId = 1;
        _model.TicketCapacity = 40;
        _model.TicketPrice = 10m;
        _model.MemberPrice = 5m;
    }

    private static List<ValidationResult> Check(object model, string propertyName, object? value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateProperty(
            value,
            new ValidationContext(model) { MemberName = propertyName },
            results);
        return results;
    }

    // ---- the form rules ----

    [Fact]
    public void Name_is_required_and_capped_at_100_characters()
    {
        Assert.NotEmpty(Check(_model, nameof(_model.Name), null));
        Assert.NotEmpty(Check(_model, nameof(_model.Name), new string('a', 101)));
        Assert.Empty(Check(_model, nameof(_model.Name), new string('a', 100)));
    }

    [Theory]
    [InlineData(nameof(EventFormPageModel.Date))]
    [InlineData(nameof(EventFormPageModel.StartTime))]
    [InlineData(nameof(EventFormPageModel.EndTime))]
    [InlineData(nameof(EventFormPageModel.VenueId))]
    [InlineData(nameof(EventFormPageModel.TicketCapacity))]
    [InlineData(nameof(EventFormPageModel.TicketPrice))]
    [InlineData(nameof(EventFormPageModel.MemberPrice))]
    public void These_fields_are_required(string propertyName)
    {
        Assert.NotEmpty(Check(_model, propertyName, null));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(1, true)]
    public void Capacity_must_be_at_least_one(int capacity, bool valid)
    {
        Assert.Equal(valid, Check(_model, nameof(_model.TicketCapacity), capacity).Count == 0);
    }

    [Fact]
    public void Prices_can_be_zero_but_not_negative()
    {
        Assert.Empty(Check(_model, nameof(_model.TicketPrice), 0m));
        Assert.NotEmpty(Check(_model, nameof(_model.TicketPrice), -0.01m));
        Assert.NotEmpty(Check(_model, nameof(_model.MemberPrice), -1m));
    }

    [Fact]
    public void Description_is_capped_at_500_characters()
    {
        Assert.Empty(Check(_model, nameof(_model.Description), new string('a', 500)));
        Assert.NotEmpty(Check(_model, nameof(_model.Description), new string('a', 501)));
    }

    // ---- saving ----

    [Fact]
    public async Task OnPost_sends_club_time_as_UTC_and_goes_to_the_events_page()
    {
        FillForm();
        CreateEventRequest? sent = null;
        _handlerMock.SetupRequest(HttpMethod.Post, "http://localhost/api/events", async request =>
            {
                sent = await request.Content!.ReadFromJsonAsync<CreateEventRequest>();
                return true;
            })
            .ReturnsResponse(HttpStatusCode.Created);

        var result = await _model.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Events/Index", redirect.PageName);
        Assert.Equal("Event added: Robot Night", _model.TempData["EventAdded"]);

        // March 4 2027 is standard time in Chicago (UTC-6): 6 PM there is midnight UTC the next day.
        Assert.NotNull(sent);
        Assert.Equal(new DateTime(2027, 3, 5, 0, 0, 0, DateTimeKind.Utc), sent.StartsAtUtc);
        Assert.Equal(new DateTime(2027, 3, 5, 2, 0, 0, DateTimeKind.Utc), sent.EndsAtUtc);
        Assert.Equal(1, sent.VenueId);
        Assert.Equal(5m, sent.MemberTicketPrice);
    }

    [Fact]
    public async Task OnPost_uses_daylight_time_in_the_summer()
    {
        FillForm();
        _model.Date = new DateOnly(2027, 7, 4);
        CreateEventRequest? sent = null;
        _handlerMock.SetupRequest(HttpMethod.Post, "http://localhost/api/events", async request =>
            {
                sent = await request.Content!.ReadFromJsonAsync<CreateEventRequest>();
                return true;
            })
            .ReturnsResponse(HttpStatusCode.Created);

        await _model.OnPostAsync(CancellationToken.None);

        // Summer is UTC-5, so 6 PM is 11 PM UTC the same day.
        Assert.NotNull(sent);
        Assert.Equal(new DateTime(2027, 7, 4, 23, 0, 0, DateTimeKind.Utc), sent.StartsAtUtc);
    }

    [Fact]
    public async Task OnPost_stops_when_the_end_is_not_after_the_start()
    {
        FillForm();
        _model.EndTime = new TimeOnly(18, 0);

        var result = await _model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(_model.ModelState.ContainsKey(nameof(_model.EndTime)));
        _handlerMock.VerifyRequest(HttpMethod.Post, "http://localhost/api/events", Times.Never());
    }

    [Fact]
    public async Task OnPost_stops_when_the_member_price_is_above_the_ticket_price()
    {
        FillForm();
        _model.MemberPrice = 20m;

        var result = await _model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(_model.ModelState.ContainsKey(nameof(_model.MemberPrice)));
    }

    [Fact]
    public async Task OnPost_stops_on_a_time_that_does_not_exist_when_clocks_spring_forward()
    {
        FillForm();
        _model.Date = new DateOnly(2027, 3, 14); // Clocks jump from 2:00 to 3:00 AM that day.
        _model.StartTime = new TimeOnly(2, 30);
        _model.EndTime = new TimeOnly(4, 0);

        var result = await _model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(_model.ModelState.ContainsKey(nameof(_model.StartTime)));
    }

    [Fact]
    public async Task OnPost_shows_API_messages_on_the_matching_form_fields()
    {
        FillForm();
        _handlerMock.SetupRequest(HttpMethod.Post, "http://localhost/api/events")
            .ReturnsJsonResponse(
                HttpStatusCode.BadRequest,
                new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["EndsAtUtc"] = ["The event has to end after it starts."],
                    ["VenueId"] = ["Pick a venue that is in the venue list."],
                }));

        var result = await _model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(_model.ModelState.ContainsKey(nameof(_model.EndTime)));
        Assert.True(_model.ModelState.ContainsKey(nameof(_model.VenueId)));
        Assert.False(_model.ModelState.ContainsKey("EndsAtUtc"));
        Assert.Null(_model.TempData["EventAdded"]);
    }

    [Fact]
    public async Task OnPost_shows_a_message_when_the_API_is_down()
    {
        FillForm();
        _handlerMock.SetupRequest(HttpMethod.Post, "http://localhost/api/events")
            .Throws(new HttpRequestException("down"));

        var result = await _model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotEmpty(_model.ModelState[string.Empty]!.Errors);
    }

    [Fact]
    public async Task OnPost_shows_a_message_on_other_API_failures()
    {
        FillForm();
        _handlerMock.SetupRequest(HttpMethod.Post, "http://localhost/api/events")
            .ReturnsResponse(HttpStatusCode.InternalServerError);

        var result = await _model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotEmpty(_model.ModelState[string.Empty]!.Errors);
    }

    // ---- club time helper ----

    [Fact]
    public void ClubTime_ToUtc_round_trips_with_ToClubTime()
    {
        var utc = ClubTime.ToUtc(new DateOnly(2027, 10, 15), new TimeOnly(18, 0));

        Assert.NotNull(utc);
        var back = ClubTime.ToClubTime(utc.Value);
        Assert.Equal(new DateTime(2027, 10, 15, 18, 0, 0), back);
    }

    [Fact]
    public void ClubTime_ToUtc_returns_null_for_a_skipped_time()
    {
        Assert.Null(ClubTime.ToUtc(new DateOnly(2027, 3, 14), new TimeOnly(2, 30)));
    }
}
