using System.Net;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Pages;
using CCOS.Web.Services;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;

namespace CCOS.Web.Tests.Pages;

/// <summary>My orders (CAR-95): orders, statuses, upcoming events, empty state and the API being down.</summary>
public class MyOrdersModelTests
{
    private const string MineUrl = "http://localhost/api/orders/mine";

    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly MyOrdersModel _model;

    public MyOrdersModelTests()
    {
        HttpClient httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost");
        _model = new MyOrdersModel(new MyOrdersApiClient(httpClient), new EventsApiClient(httpClient), new Mock<ILogger<MyOrdersModel>>().Object);
    }

    [Fact]
    public async Task Orders_show_in_the_order_the_API_sends_them_with_their_items_and_status()
    {
        CartTestContext.Attach(_model, member: true);
        RespondWithOrders(
            new OrderDto(42, DateTime.UtcNow, "Pending", 28m, [new OrderLineDto(1, "Club Hoodie", null, 1, 28m, 28m)]),
            new OrderDto(17, DateTime.UtcNow.AddDays(-3), "Canceled", 12m, [new OrderLineDto(2, "Club T-Shirt", null, 1, 12m, 12m)]));

        await _model.OnGetAsync(CancellationToken.None);

        Assert.False(_model.LoadFailed);
        Assert.Equal(new[] { 42, 17 }, _model.Orders.Select(order => order.OrderId));
        MyOrdersModel.OrderSummary newest = _model.Orders[0];
        Assert.Equal("Pending (pay in person)", newest.StatusText);
        Assert.Equal("pending", newest.StatusKey);
        Assert.Equal("$28.00", newest.TotalText);
        Assert.Equal("Club Hoodie", Assert.Single(newest.Items).Name);
        Assert.Equal("Canceled", _model.Orders[1].StatusText);
    }

    [Fact]
    public async Task Upcoming_events_add_up_tickets_and_skip_canceled_orders_and_past_events()
    {
        CartTestContext.Attach(_model);
        RespondWithOrders(
            new OrderDto(3, DateTime.UtcNow, "Pending", 0m, [new OrderLineDto(12, "General", 5, 1, 0m, 0m)]),
            new OrderDto(2, DateTime.UtcNow.AddDays(-1), "Pending", 10m, [new OrderLineDto(12, "General", 5, 2, 5m, 10m), new OrderLineDto(1, "Club Hoodie", null, 1, 35m, 35m)]),
            new OrderDto(1, DateTime.UtcNow.AddDays(-2), "Canceled", 3m, [new OrderLineDto(30, "General", 9, 1, 3m, 3m)]));
        RespondWithEvent(new EventDto(5, "Capture the Flag Night", null, DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(7).AddHours(2),
            50, false, true, 1, "Cougar Commons", [], 1));

        await _model.OnGetAsync(CancellationToken.None);

        MyOrdersModel.UpcomingEvent upcoming = Assert.Single(_model.UpcomingEvents);
        Assert.Equal("Capture the Flag Night", upcoming.Name);
        Assert.Equal(3, upcoming.Tickets);
        Assert.Equal("Cougar Commons", upcoming.VenueName);
    }

    [Fact]
    public async Task An_event_that_is_over_or_removed_is_left_out_of_upcoming()
    {
        CartTestContext.Attach(_model);
        RespondWithOrders(new OrderDto(1, DateTime.UtcNow, "Pending", 0m, [new OrderLineDto(12, "General", 5, 1, 0m, 0m)]));
        _mockHttp.When(HttpMethod.Get, "http://localhost/api/events/5").Respond(HttpStatusCode.NotFound);

        await _model.OnGetAsync(CancellationToken.None);

        Assert.Single(_model.Orders);
        Assert.Empty(_model.UpcomingEvents);
    }

    [Fact]
    public async Task No_orders_shows_the_empty_state()
    {
        CartTestContext.Attach(_model);
        RespondWithOrders();

        await _model.OnGetAsync(CancellationToken.None);

        Assert.False(_model.LoadFailed);
        Assert.Empty(_model.Orders);
        Assert.Empty(_model.UpcomingEvents);
    }

    [Fact]
    public async Task The_page_says_so_when_the_API_is_down()
    {
        CartTestContext.Attach(_model);
        _mockHttp.When(HttpMethod.Get, MineUrl).Throw(new HttpRequestException("down"));

        await _model.OnGetAsync(CancellationToken.None);

        Assert.True(_model.LoadFailed);
    }

    [Theory]
    [InlineData("Pending", "Pending (pay in person)")]
    [InlineData("Paid", "Paid")]
    [InlineData("Fulfilled", "Picked up")]
    [InlineData("Canceled", "Canceled")]
    [InlineData("OnHold", "OnHold")]
    public void StatusText_says_each_status_in_plain_words(string status, string expected)
    {
        Assert.Equal(expected, MyOrdersModel.StatusText(status));
    }

    private void RespondWithOrders(params OrderDto[] orders) =>
        _mockHttp.When(HttpMethod.Get, MineUrl).Respond("application/json", JsonSerializer.Serialize(orders));

    private void RespondWithEvent(EventDto dto) =>
        _mockHttp.When(HttpMethod.Get, $"http://localhost/api/events/{dto.EventId}")
            .Respond("application/json", JsonSerializer.Serialize(dto));
}
