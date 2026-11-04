using System.Net;
using System.Net.Http.Json;
using CCOS.Shared;
using CCOS.Web.Pages.Officer.Venues;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Contrib.HttpClient;

namespace CCOS.Web.Tests.Pages.Officer.Venues;

/// <summary>
/// Tests for the Edit Venue page: loading, saving through PUT, and Remove/Restore with the
/// "Show in the venue list" box (CAR-57, CAR-58, CAR-63).
/// </summary>
public class EditModelTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly EditModel _model;

    public EditModelTests()
    {
        var httpClient = _handlerMock.CreateClient();
        httpClient.BaseAddress = new Uri("http://localhost");

        _model = new EditModel(new VenuesApiClient(httpClient), new Mock<ILogger<EditModel>>().Object)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
        };
    }

    // Fills the form the way an officer would, so each test only changes what it checks.
    private void FillForm(string zipCode = "54481", bool isActive = true)
    {
        _model.Name = "Main Hall";
        _model.AddressLine1 = "1001 Centerpoint Dr";
        _model.City = "Stevens Point";
        _model.State = "WI";
        _model.ZipCode = zipCode;
        _model.Capacity = 100;
        _model.IsActive = isActive;
    }

    [Fact]
    public async Task OnGet_fills_the_form_from_the_venue()
    {
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/venues/4")
            .ReturnsJsonResponse(new VenueDetailDto(4, "Main Hall", 100, false, "1 Main St", "Room 2", "Plover", "WI", "54467-0001"));

        await _model.OnGetAsync(4, CancellationToken.None);

        Assert.True(_model.Found);
        Assert.Equal("Main Hall", _model.Name);
        Assert.Equal("54467-0001", _model.ZipCode);
        Assert.False(_model.IsActive);
        Assert.True(_model.ShowActiveBox);
    }

    [Fact]
    public async Task OnGet_sends_404_for_an_unknown_venue()
    {
        _handlerMock.SetupRequest(HttpMethod.Get, "http://localhost/api/venues/4")
            .ReturnsResponse(HttpStatusCode.NotFound);

        await _model.OnGetAsync(4, CancellationToken.None);

        Assert.False(_model.Found);
        Assert.Equal(StatusCodes.Status404NotFound, _model.Response.StatusCode);
    }

    [Fact]
    public async Task OnPost_sends_a_PUT_and_goes_back_to_the_list()
    {
        FillForm();
        UpdateVenueRequest? sent = null;
        _handlerMock.SetupRequest(HttpMethod.Put, "http://localhost/api/venues/4", async request =>
            {
                sent = await request.Content!.ReadFromJsonAsync<UpdateVenueRequest>();
                return true;
            })
            .ReturnsJsonResponse(new VenueDetailDto(4, "Main Hall", 100, true, null, null, null, null, null));

        var result = await _model.OnPostAsync(4, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Officer/Venues/Index", redirect.PageName);
        Assert.NotNull(sent);
        Assert.True(sent.IsActive);
        Assert.Equal("Venue saved: Main Hall", _model.TempData["VenueSaved"]);
    }

    [Fact]
    public async Task OnPost_with_the_box_unticked_removes_the_venue_and_says_so()
    {
        FillForm(isActive: false);
        UpdateVenueRequest? sent = null;
        _handlerMock.SetupRequest(HttpMethod.Put, "http://localhost/api/venues/4", async request =>
            {
                sent = await request.Content!.ReadFromJsonAsync<UpdateVenueRequest>();
                return true;
            })
            .ReturnsJsonResponse(new VenueDetailDto(4, "Main Hall", 100, false, null, null, null, null, null));

        await _model.OnPostAsync(4, CancellationToken.None);

        Assert.NotNull(sent);
        Assert.False(sent.IsActive);
        Assert.Contains("Venue removed", (string)_model.TempData["VenueSaved"]!);
        Assert.Contains("restore", (string)_model.TempData["VenueSaved"]!);
    }

    [Fact]
    public async Task OnPost_shows_the_api_message_when_the_name_is_taken()
    {
        FillForm();
        _handlerMock.SetupRequest(HttpMethod.Put, "http://localhost/api/venues/4")
            .ReturnsJsonResponse(
                HttpStatusCode.BadRequest,
                new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["Name"] = ["A venue with this name already exists."],
                }));

        var result = await _model.OnPostAsync(4, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(_model.Found);
        Assert.Equal(
            "A venue with this name already exists.",
            _model.ModelState["Name"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task OnPost_does_not_call_the_api_when_the_form_is_invalid()
    {
        FillForm(zipCode: "1234");
        _model.ModelState.AddModelError("ZipCode", "bad");

        var result = await _model.OnPostAsync(4, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        _handlerMock.VerifyAnyRequest(Times.Never());
    }

    [Fact]
    public async Task OnPost_shows_an_error_when_the_api_is_down()
    {
        FillForm();
        _handlerMock.SetupAnyRequest().ThrowsAsync(new HttpRequestException("down"));

        var result = await _model.OnPostAsync(4, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Contains("could not be reached", _model.ModelState[string.Empty]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task OnPost_sends_404_when_the_venue_is_gone()
    {
        FillForm();
        _handlerMock.SetupRequest(HttpMethod.Put, "http://localhost/api/venues/4")
            .ReturnsResponse(HttpStatusCode.NotFound);

        var result = await _model.OnPostAsync(4, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(_model.Found);
        Assert.Equal(StatusCodes.Status404NotFound, _model.Response.StatusCode);
    }
}
