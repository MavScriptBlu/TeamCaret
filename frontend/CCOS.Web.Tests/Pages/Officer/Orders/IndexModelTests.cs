using System.Net;
using System.Net.Http.Json;
using CCOS.Shared;
using CCOS.Web.Pages.Officer.Orders;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Contrib.HttpClient;

namespace CCOS.Web.Tests.Pages.Officer.Orders;

public sealed class IndexModelTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly IndexModel _model;

    public IndexModelTests()
    {
        HttpClient client = _handlerMock.CreateClient();
        client.BaseAddress = new Uri("http://localhost");
        _model = new IndexModel(new OrdersApiClient(client), new Mock<ILogger<IndexModel>>().Object)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
        };
    }

    [Fact]
    public async Task OnGet_requests_selected_status_from_api()
    {
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/orders?status=Paid")
            .ReturnsJsonResponse(new List<OfficerOrderDto> { Order(7, OrderStatuses.Paid) });
        _model.Status = OrderStatuses.Paid;

        await _model.OnGetAsync(CancellationToken.None);

        Assert.False(_model.LoadFailed);
        Assert.Equal(OrderStatuses.Paid, _model.Status);
        Assert.Equal(7, Assert.Single(_model.Orders).OrderId);
        _handlerMock.VerifyRequest(HttpMethod.Get, "http://localhost/api/orders?status=Paid", Times.Once());
    }

    [Fact]
    public async Task OnGet_with_invalid_status_loads_all_orders()
    {
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/orders")
            .ReturnsJsonResponse(new List<OfficerOrderDto> { Order(7, OrderStatuses.Pending) });
        _model.Status = "Refunded";

        await _model.OnGetAsync(CancellationToken.None);

        Assert.Null(_model.Status);
        Assert.Single(_model.Orders);
        _handlerMock.VerifyRequest(HttpMethod.Get, "http://localhost/api/orders", Times.Once());
    }

    [Fact]
    public async Task OnGet_sets_load_failed_when_api_is_unavailable()
    {
        _handlerMock.SetupAnyRequest().ThrowsAsync(new HttpRequestException("Connection refused"));

        await _model.OnGetAsync(CancellationToken.None);

        Assert.True(_model.LoadFailed);
        Assert.Empty(_model.Orders);
    }

    [Fact]
    public async Task OnPostPay_posts_status_and_version_then_redirects_with_filter()
    {
        ChangeOrderStatusRequest? sent = null;
        _handlerMock.SetupRequest(HttpMethod.Put, "http://localhost/api/orders/7/status", async request =>
            {
                sent = await request.Content!.ReadFromJsonAsync<ChangeOrderStatusRequest>();
                return true;
            })
            .ReturnsResponse(HttpStatusCode.OK);

        IActionResult result = await _model.OnPostPayAsync(7, OrderStatuses.Pending, 42, CancellationToken.None);

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Officer/Orders/Index", redirect.PageName);
        Assert.Equal(OrderStatuses.Pending, redirect.RouteValues!["status"]);
        Assert.NotNull(sent);
        Assert.Equal(OrderStatuses.Paid, sent.Status);
        Assert.Equal(42u, sent.RowVersion);
        Assert.Equal("Order marked as paid.", _model.TempData["OrderMessage"]);
    }

    [Fact]
    public async Task OnPostCancel_turns_conflict_into_friendly_message_and_keeps_filter()
    {
        _handlerMock.SetupRequest(HttpMethod.Put, "http://localhost/api/orders/7/status")
            .ReturnsResponse(HttpStatusCode.Conflict);

        IActionResult result = await _model.OnPostCancelAsync(7, OrderStatuses.Paid, 42, CancellationToken.None);

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(OrderStatuses.Paid, redirect.RouteValues!["status"]);
        Assert.Contains("changed or that status change is not allowed", (string)_model.TempData["OrderError"]!);
        Assert.False(_model.TempData.ContainsKey("OrderMessage"));
    }

    [Fact]
    public async Task OnPostPickup_handles_unavailable_api_without_throwing()
    {
        _handlerMock.SetupAnyRequest().ThrowsAsync(new HttpRequestException("Connection refused"));

        IActionResult result = await _model.OnPostPickupAsync(7, OrderStatuses.Paid, 42, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Contains("could not be reached", (string)_model.TempData["OrderError"]!);
    }

    [Fact]
    public async Task OnPostCancel_rejects_missing_version_without_calling_api()
    {
        await _model.OnPostCancelAsync(7, OrderStatuses.Paid, null, CancellationToken.None);

        Assert.Contains("could not be identified", (string)_model.TempData["OrderError"]!);
        _handlerMock.VerifyAnyRequest(Times.Never());
    }

    private static OfficerOrderDto Order(int id, string status) => new(
        id,
        "A. Customer",
        new DateTime(2026, 11, 20, 17, 0, 0, DateTimeKind.Utc),
        [new OfficerOrderLineDto("Saved shirt", 1, 10m, 10m)],
        10m,
        status,
        42);
}
