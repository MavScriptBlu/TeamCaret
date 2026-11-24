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

/// <summary>The ticket Add to cart button on the event page (CAR-92).</summary>
public class DetailsAddToCartTests
{
    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly DetailsModel _model;

    public DetailsAddToCartTests()
    {
        HttpClient httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost");
        _model = new DetailsModel(new EventsApiClient(httpClient), new Mock<ILogger<DetailsModel>>().Object);
    }

    [Fact]
    public async Task Add_to_cart_adds_tickets_and_comes_back_to_the_event()
    {
        HttpContext http = CartTestContext.Attach(_model);
        RespondWith(CartModelTests.GameNight(capacity: 50, General));

        IActionResult result = await _model.OnPostAddToCartAsync(5, 12, 2, CancellationToken.None);

        Assert.Equal("/Events/Details", Assert.IsType<RedirectToPageResult>(result).PageName);
        var item = Assert.Single(http.GetCart().Items);
        Assert.Equal(new CartItem(CartItemKind.Ticket, 12, 5, 2), item);
        Assert.NotNull(_model.TempData[CartMessages.MessageKey]);
    }

    [Fact]
    public async Task Tickets_never_go_over_the_seats_counting_other_ticket_types()
    {
        HttpContext http = CartTestContext.Attach(_model, new Cart([new CartItem(CartItemKind.Ticket, 12, 5, 3)]));
        RespondWith(CartModelTests.GameNight(capacity: 5, General, new TicketTypeDto(13, "Student", 2m, null, true)));

        await _model.OnPostAddToCartAsync(5, 13, 4, CancellationToken.None);

        Assert.Equal(2, http.GetCart().QuantityOf(CartItemKind.Ticket, 13));
        Assert.Equal(5, http.GetCart().TicketsFor(5));
    }

    [Fact]
    public async Task Members_only_tickets_need_a_current_member()
    {
        HttpContext http = CartTestContext.Attach(_model);
        RespondWith(CartModelTests.GameNight(capacity: 50, General) with { MembersOnly = true });

        await _model.OnPostAddToCartAsync(5, 12, 1, CancellationToken.None);

        Assert.True(http.GetCart().IsEmpty);
        Assert.NotNull(_model.TempData[CartMessages.ErrorKey]);
    }

    [Fact]
    public async Task A_member_can_add_members_only_tickets()
    {
        HttpContext http = CartTestContext.Attach(_model, member: true);
        RespondWith(CartModelTests.GameNight(capacity: 50, General) with { MembersOnly = true });

        await _model.OnPostAddToCartAsync(5, 12, 1, CancellationToken.None);

        Assert.Equal(1, http.GetCart().Count);
    }

    [Fact]
    public async Task Tickets_for_an_event_that_has_started_cannot_be_added()
    {
        // Matches POST /api/orders (CAR-91): sales close at the start time, even while the event is still on.
        HttpContext http = CartTestContext.Attach(_model);
        var started = CartModelTests.GameNight(capacity: 50, General) with
        {
            StartsAtUtc = DateTime.UtcNow.AddHours(-1),
            EndsAtUtc = DateTime.UtcNow.AddHours(2),
        };
        RespondWith(started);

        await _model.OnPostAddToCartAsync(5, 12, 1, CancellationToken.None);

        Assert.True(http.GetCart().IsEmpty);
        Assert.Equal("Ticket sales have closed for this event.", _model.TempData[CartMessages.ErrorKey]);
    }

    private static TicketTypeDto General => new(12, "General", 5m, null, true);

    private void RespondWith(EventDto dto) =>
        _mockHttp.When(HttpMethod.Get, "http://localhost/api/events/5")
            .Respond("application/json", JsonSerializer.Serialize(dto));
}
