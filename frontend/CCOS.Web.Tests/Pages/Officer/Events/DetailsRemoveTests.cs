using System.Net;
using CCOS.Web.Pages.Events;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Contrib.HttpClient;

namespace CCOS.Web.Tests.Pages.Officer.Events;

/// <summary>
/// Tests for the Remove event button on the event page (CAR-56).
/// </summary>
public class DetailsRemoveTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly DetailsModel _model;

    public DetailsRemoveTests()
    {
        var httpClient = _handlerMock.CreateClient();
        httpClient.BaseAddress = new Uri("http://localhost");

        _model = new DetailsModel(new EventsApiClient(httpClient), new Mock<ILogger<DetailsModel>>().Object)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
        };
    }

    [Fact]
    public async Task OnPostRemove_sends_a_DELETE_and_goes_to_the_events_page()
    {
        _handlerMock.SetupRequest(HttpMethod.Delete, "http://localhost/api/events/5")
            .ReturnsResponse(HttpStatusCode.NoContent);

        var result = await _model.OnPostRemoveAsync(5, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Events/Index", redirect.PageName);
        Assert.NotNull(_model.TempData["EventRemoved"]);
    }

    [Fact]
    public async Task OnPostRemove_stays_on_the_event_page_when_the_API_fails()
    {
        _handlerMock.SetupRequest(HttpMethod.Delete, "http://localhost/api/events/5")
            .ReturnsResponse(HttpStatusCode.InternalServerError);

        var result = await _model.OnPostRemoveAsync(5, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Events/Details", redirect.PageName);
        Assert.NotNull(_model.TempData["EventError"]);
    }

    [Fact]
    public async Task OnPostRemove_goes_to_the_events_page_when_the_event_is_already_gone()
    {
        _handlerMock.SetupRequest(HttpMethod.Delete, "http://localhost/api/events/5")
            .ReturnsResponse(HttpStatusCode.NotFound);

        var result = await _model.OnPostRemoveAsync(5, CancellationToken.None);

        Assert.Equal("/Events/Index", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.NotNull(_model.TempData["EventError"]);
    }
}
