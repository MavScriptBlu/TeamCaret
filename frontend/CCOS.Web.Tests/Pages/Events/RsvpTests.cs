using System.Net;
using System.Security.Claims;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Pages.Events;
using CCOS.Web.Services;
using CCOS.Web.Tests.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;

namespace CCOS.Web.Tests.Pages.Events;

/// <summary>RSVP on the event page (CAR-94): what the button shows, RSVP, paid events, members only and cancel.</summary>
public class RsvpTests
{
    private const string RsvpUrl = "http://localhost/api/events/5/rsvp";

    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly DetailsModel _model;
    private readonly RsvpApiClient _rsvpApi;

    public RsvpTests()
    {
        HttpClient httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost");
        _model = new DetailsModel(new EventsApiClient(httpClient), new Mock<ILogger<DetailsModel>>().Object);
        _rsvpApi = new RsvpApiClient(httpClient);
    }

    [Theory]
    [InlineData(true, false, false, 0, 0, 30, DetailsModel.RsvpView.Free)]
    [InlineData(false, false, false, 0, 0, 30, DetailsModel.RsvpView.SignIn)]
    [InlineData(true, false, true, 0, 0, 30, DetailsModel.RsvpView.Going)]
    [InlineData(true, false, false, 10, 10, 30, DetailsModel.RsvpView.Paid)]
    [InlineData(true, false, false, 0, 30, 30, DetailsModel.RsvpView.SoldOut)]
    [InlineData(false, false, false, 0, 30, 30, DetailsModel.RsvpView.SoldOut)]
    public void The_button_matches_the_visitor_and_the_event(bool signedIn, bool member, bool going, int price, int sold, int capacity, DetailsModel.RsvpView expected)
    {
        EventInfo eventInfo = Event(price, sold, capacity);

        Assert.Equal(expected, DetailsModel.RsvpViewFor(eventInfo, signedIn, member, going, DateTime.UtcNow));
    }

    [Fact]
    public void Members_only_events_say_so_to_non_members()
    {
        EventInfo eventInfo = Event(0, 0, 30) with { MembersOnly = true };

        Assert.Equal(DetailsModel.RsvpView.MembersOnly, DetailsModel.RsvpViewFor(eventInfo, true, false, false, DateTime.UtcNow));
        Assert.Equal(DetailsModel.RsvpView.Free, DetailsModel.RsvpViewFor(eventInfo, true, true, false, DateTime.UtcNow));
    }

    [Fact]
    public void Nothing_shows_once_the_event_has_started()
    {
        EventInfo started = Event(0, 0, 30) with { StartsAtUtc = DateTime.UtcNow.AddHours(-1) };

        Assert.Equal(DetailsModel.RsvpView.Hidden, DetailsModel.RsvpViewFor(started, true, false, false, DateTime.UtcNow));
    }

    [Fact]
    public async Task RSVP_to_a_free_event_says_you_are_going()
    {
        SignIn();
        RespondWithEvent(price: 0m);
        MockedRequest post = _mockHttp.When(HttpMethod.Post, RsvpUrl);
        post.Respond("application/json", JsonSerializer.Serialize(new RsvpStatusDto(true, 1, true)));

        IActionResult result = await _model.OnPostRsvpAsync(5, _rsvpApi, CancellationToken.None);

        Assert.Equal("/Events/Details", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.Equal(1, _mockHttp.GetMatchCount(post));
        Assert.Equal("You're going to Study Night! It's in My orders too.", _model.TempData[DetailsModel.RsvpMessageKey]);
    }

    [Fact]
    public async Task RSVP_to_a_paid_event_adds_a_ticket_to_the_cart()
    {
        HttpContext http = SignIn();
        RespondWithEvent(price: 10m);
        MockedRequest post = _mockHttp.When(HttpMethod.Post, RsvpUrl);
        post.Respond(HttpStatusCode.OK);

        IActionResult result = await _model.OnPostRsvpAsync(5, _rsvpApi, CancellationToken.None);

        Assert.Equal("/Cart", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.Equal(0, _mockHttp.GetMatchCount(post));
        Assert.Equal(1, http.GetCart().QuantityOf(CartItemKind.Ticket, 12));
    }

    [Fact]
    public async Task A_refused_RSVP_shows_the_APIs_reason()
    {
        SignIn();
        RespondWithEvent(price: 0m);
        _mockHttp.When(HttpMethod.Post, RsvpUrl).Respond(HttpStatusCode.Forbidden, "application/problem+json",
            """{"title":"Members only","status":403,"detail":"Hackathon is for members only."}""");

        await _model.OnPostRsvpAsync(5, _rsvpApi, CancellationToken.None);

        Assert.Equal("Hackathon is for members only.", _model.TempData[CartMessages.ErrorKey]);
    }

    [Fact]
    public async Task Signed_out_RSVP_goes_to_log_in_and_back()
    {
        CartTestContext.Attach(_model);

        IActionResult result = await _model.OnPostRsvpAsync(5, _rsvpApi, CancellationToken.None);

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Account/Login", redirect.PageName);
        Assert.Equal("/Events/Details/5", redirect.RouteValues?["returnUrl"]);
    }

    [Fact]
    public async Task Cancel_RSVP_says_the_seat_was_given_back()
    {
        SignIn();
        _mockHttp.When(HttpMethod.Delete, RsvpUrl).Respond(HttpStatusCode.NoContent);

        await _model.OnPostCancelRsvpAsync(5, _rsvpApi, CancellationToken.None);

        Assert.Equal("Your RSVP is canceled and your seat was given back.", _model.TempData[DetailsModel.RsvpMessageKey]);
    }

    [Fact]
    public async Task When_the_API_is_down_RSVP_says_try_again()
    {
        SignIn();
        RespondWithEvent(price: 0m);
        _mockHttp.When(HttpMethod.Post, RsvpUrl).Throw(new HttpRequestException("down"));

        await _model.OnPostRsvpAsync(5, _rsvpApi, CancellationToken.None);

        Assert.NotNull(_model.TempData[CartMessages.ErrorKey]);
    }

    // Signs in a guest (not a member) on the page.
    private HttpContext SignIn()
    {
        HttpContext http = CartTestContext.Attach(_model);
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Guest")], "Test"));
        return http;
    }

    private void RespondWithEvent(decimal price) =>
        _mockHttp.When(HttpMethod.Get, "http://localhost/api/events/5")
            .Respond("application/json", JsonSerializer.Serialize(new EventDto(
                5, "Study Night", null, DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(7).AddHours(2),
                30, false, true, 1, "Library", [new TicketTypeDto(12, "General", price, null, true)], 1)));

    private static EventInfo Event(int price, int sold, int capacity) => new(
        5, "Study Night", null, DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(7).AddHours(2),
        capacity, false, true, 1, "Library", [new TicketTypeInfo("General", price, price, true, 12)], [], sold);
}
