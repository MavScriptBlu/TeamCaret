using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Pages.Account;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RichardSzalay.MockHttp;

namespace CCOS.Web.Tests.Pages.Account;

/// <summary>Log in, Create account and Log out pages, plus the token handler (CAR-68).</summary>
public class AccountPagesTests
{
    private const string Email = "member@ccos.test";

    // Test-only value; the API is faked, so it is never checked against a real account.
    private static readonly string TestSecret = $"T{Guid.NewGuid():N}!a1";

    private readonly MockHttpMessageHandler _api = new();
    private readonly Mock<IAuthenticationService> _auth = new();

    private AccountApiClient CreateClient()
    {
        var http = _api.ToHttpClient();
        http.BaseAddress = new Uri("http://localhost/");
        return new AccountApiClient(http);
    }

    private PageContext CreatePageContext()
    {
        var services = new ServiceCollection()
            .AddSingleton(_auth.Object)
            .BuildServiceProvider();

        return new PageContext { HttpContext = new DefaultHttpContext { RequestServices = services } };
    }

    private static string TokensJson() => JsonSerializer.Serialize(new
    {
        tokenType = "Bearer",
        accessToken = "access-123",
        expiresIn = 3600,
        refreshToken = "refresh-456",
    });

    private static string MeJson() => JsonSerializer.Serialize(
        new CurrentAccountDto(7, "Morgan", "Lee", Email, MemberStatus.Active, true, false, null, false),
        new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private LoginModel CreateLogin(string? returnUrl = null) =>
        new(CreateClient(), NullLogger<LoginModel>.Instance)
        {
            PageContext = CreatePageContext(),
            Email = Email,
            Password = TestSecret,
            ReturnUrl = returnUrl,
        };

    private RegisterModel CreateRegister() =>
        new(CreateClient(), NullLogger<RegisterModel>.Instance)
        {
            PageContext = CreatePageContext(),
            FirstName = " Morgan ",
            LastName = "Lee",
            Email = Email,
            Password = TestSecret,
            ConfirmPassword = TestSecret,
        };

    [Fact]
    public async Task Login_WrongPassword_ShowsOneGeneralMessage()
    {
        _api.When(HttpMethod.Post, "http://localhost/api/auth/login").Respond(HttpStatusCode.Unauthorized);
        var model = CreateLogin();

        var result = await model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(LoginModel.WrongCredentialsMessage, model.ModelState[string.Empty]!.Errors.Single().ErrorMessage);
        _auth.Verify(a => a.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string?>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties?>()), Times.Never);
    }

    [Fact]
    public async Task Login_RightPassword_SignsInWithTokensAndGoesBack()
    {
        _api.When(HttpMethod.Post, "http://localhost/api/auth/login").Respond("application/json", TokensJson());
        _api.When(HttpMethod.Get, "http://localhost/api/me")
            .WithHeaders("Authorization", "Bearer access-123")
            .Respond("application/json", MeJson());

        ClaimsPrincipal? signedIn = null;
        AuthenticationProperties? properties = null;
        _auth.Setup(a => a.SignInAsync(It.IsAny<HttpContext>(), CookieAuthenticationDefaults.AuthenticationScheme, It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties?>()))
            .Callback<HttpContext, string?, ClaimsPrincipal, AuthenticationProperties?>((_, _, p, props) => { signedIn = p; properties = props; })
            .Returns(Task.CompletedTask);

        var model = CreateLogin("/Events");
        var result = await model.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Events", redirect.Url);
        Assert.Equal("Morgan", signedIn!.Identity!.Name);
        Assert.Equal("7", signedIn.FindFirstValue(AccountSession.CustomerIdClaim));
        Assert.Equal("access-123", properties!.GetTokenValue(AccountSession.AccessTokenName));
        Assert.Equal("refresh-456", properties.GetTokenValue(AccountSession.RefreshTokenName));
    }

    [Fact]
    public async Task Login_ApiDown_ShowsTryAgainLater()
    {
        _api.When("*").Throw(new HttpRequestException("down"));
        var model = CreateLogin();

        var result = await model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Contains("isn't available right now", model.ModelState[string.Empty]!.Errors.Single().ErrorMessage);
    }

    [Fact]
    public async Task Register_EmailTaken_ShowsMessageNextToEmail()
    {
        var problem = JsonSerializer.Serialize(new
        {
            title = "One or more validation errors occurred.",
            status = 400,
            errors = new Dictionary<string, string[]> { ["Email"] = ["Username 'member@ccos.test' is already taken."] },
        });
        _api.When(HttpMethod.Post, "http://localhost/api/account/register")
            .Respond(HttpStatusCode.BadRequest, "application/problem+json", problem);
        var model = CreateRegister();

        var result = await model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Contains("already taken", model.ModelState["Email"]!.Errors.Single().ErrorMessage);
    }

    [Fact]
    public async Task Register_Success_TrimsNamesAndSignsIn()
    {
        _api.When(HttpMethod.Post, "http://localhost/api/account/register")
            .WithPartialContent("\"firstName\":\"Morgan\"")
            .Respond(HttpStatusCode.OK);
        _api.When(HttpMethod.Post, "http://localhost/api/auth/login").Respond("application/json", TokensJson());
        _api.When(HttpMethod.Get, "http://localhost/api/me").Respond("application/json", MeJson());
        var model = CreateRegister();

        var result = await model.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Products", redirect.Url);
        _auth.Verify(a => a.SignInAsync(It.IsAny<HttpContext>(), CookieAuthenticationDefaults.AuthenticationScheme, It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties?>()), Times.Once);
    }

    [Fact]
    public async Task Logout_Post_SignsOutAndGoesToMerch()
    {
        var model = new LogoutModel { PageContext = CreatePageContext() };

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Products/Index", redirect.PageName);
        _auth.Verify(a => a.SignOutAsync(It.IsAny<HttpContext>(), CookieAuthenticationDefaults.AuthenticationScheme, It.IsAny<AuthenticationProperties?>()), Times.Once);
    }

    [Theory]
    [InlineData("/Events", "/Events")]
    [InlineData("https://evil.example/", "/Products")]
    [InlineData("//evil.example/", "/Products")]
    [InlineData("/\\evil.example/", "/Products")]
    [InlineData(null, "/Products")]
    public void SafeReturnUrl_OnlyAllowsPagesOnThisSite(string? returnUrl, string expected)
    {
        Assert.Equal(expected, AccountSession.SafeReturnUrl(returnUrl));
    }

    [Fact]
    public async Task BearerHandler_SignedIn_AddsTheTokenToApiCalls()
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = AccountSession.AccessTokenName, Value = "access-123" }]);
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Morgan")], CookieAuthenticationDefaults.AuthenticationScheme));
        _auth.Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string?>()))
            .ReturnsAsync(AuthenticateResult.Success(new AuthenticationTicket(user, properties, CookieAuthenticationDefaults.AuthenticationScheme)));

        var httpContext = CreatePageContext().HttpContext;
        httpContext.User = user;

        AuthenticationHeaderValue? sent = null;
        var inner = new MockHttpMessageHandler();
        inner.When("*").With(r => { sent = r.Headers.Authorization; return true; }).Respond(HttpStatusCode.OK);

        var handler = new ApiBearerTokenHandler(new HttpContextAccessor { HttpContext = httpContext }) { InnerHandler = inner };
        using var client = new HttpClient(handler);
        await client.GetAsync("http://localhost/api/products");

        Assert.Equal("Bearer", sent!.Scheme);
        Assert.Equal("access-123", sent.Parameter);
    }

    [Fact]
    public async Task BearerHandler_SignedOut_SendsNoToken()
    {
        AuthenticationHeaderValue? sent = null;
        var inner = new MockHttpMessageHandler();
        inner.When("*").With(r => { sent = r.Headers.Authorization; return true; }).Respond(HttpStatusCode.OK);

        var handler = new ApiBearerTokenHandler(new HttpContextAccessor { HttpContext = new DefaultHttpContext() }) { InnerHandler = inner };
        using var client = new HttpClient(handler);
        await client.GetAsync("http://localhost/api/products");

        Assert.Null(sent);
    }
}
