using CCOS.Shared;
using CCOS.Web.Filters;
using CCOS.Web.Health;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// The API's address, from Api:BaseUrl: appsettings.json locally, or the Api__BaseUrl environment
// variable on Docker or a host (CAR-84). Every API client below uses it.
Uri ApiBaseAddress() => new(builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException(
        "Api:BaseUrl is missing. Set it in appsettings.json or the Api__BaseUrl environment variable."));

// ---- Services (things the app can use) ----
// Turns the .cshtml files under Pages into web pages.
// Every page under /Officer needs the Officer policy (CAR-69). A visitor who isn't signed in goes to
// Log in ("Log in first"); a signed-in person who isn't an officer sees "Officers only".
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Officer", ClubPolicies.Officer);
})
// Safety net (CAR-60): a page that doesn't catch "the API can't be reached" answers 503
// with the friendly status page instead of crashing.
.AddMvcOptions(options => options.Filters.Add<ApiUnavailablePageFilter>());

// The website's Officer policy reads the officer flag in the sign-in cookie (an advisor counts too).
// It only decides which pages open; the API checks the database on every call (CAR-67).
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(ClubPolicies.Officer, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(AccountSession.OfficerClaim, "true"));

// ---- Sign-in (CAR-68) ----
// The API does the real checking; the website remembers who's signed in with an encrypted cookie.
// The cookie also holds the API's sign-in token, which never reaches the browser's JavaScript.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ccos.signin";
        options.Cookie.HttpOnly = true;
        // https-only when the site runs on https (Azure); Docker serves plain http on localhost:8080.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        // Same as the API's refresh token (14 days); the API token inside is renewed as needed.
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        // Renews the API token before it runs out, and signs the person out if it can't.
        options.Events.OnValidatePrincipal = AccountSession.ValidateAsync;
        // A locked page sent the person to Log in: tell the page to say "Log in first" (CAR-69).
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.Redirect(context.RedirectUri + "&loginFirst=true");
            return Task.CompletedTask;
        };
    });

// Adds the signed-in person's API token to calls from the Products, Events and Venues clients.
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ApiBearerTokenHandler>();

// Create account, log in, refresh and "who am I?" (no bearer handler: see AccountApiClient).
builder.Services.AddHttpClient<AccountApiClient>(client =>
{
    client.BaseAddress = ApiBaseAddress();
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Product data comes from CCOS.Api. Run the API alongside the site (see Api:BaseUrl).
// Pages ask for a ProductsApiClient and get one that already knows the API address.
builder.Services.AddHttpClient<ProductsApiClient>(client =>
{
    client.BaseAddress = ApiBaseAddress();
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<ApiBearerTokenHandler>();

// Event data comes from CCOS.Api too (Events list, event page and Edit Event).
builder.Services.AddHttpClient<EventsApiClient>(client =>
{
    client.BaseAddress = ApiBaseAddress();
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<ApiBearerTokenHandler>();

// Venue data comes from CCOS.Api too (venue form and the event form's venue dropdown).
builder.Services.AddHttpClient<VenuesApiClient>(client =>
{
    client.BaseAddress = ApiBaseAddress();
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<ApiBearerTokenHandler>();

// GET /health (CAR-84): "Healthy" when the API answers its own /health (so the API and its
// database work too), "Unhealthy" (503) when it doesn't. Hosts use it to check the site is up.
builder.Services.AddHttpClient(ApiHealthCheck.ClientName, client =>
{
    client.BaseAddress = ApiBaseAddress();
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHealthChecks()
    .AddCheck<ApiHealthCheck>("api");

// Sign-in cookies are encrypted with keys the site makes on its own. In a container those keys
// are lost on every restart, which signs everyone out. On a host, set DataProtection:KeysPath
// (environment variable DataProtection__KeysPath) to a folder that survives restarts (CAR-84).
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("CCOS.Web")
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

var app = builder.Build();

// ---- Request pipeline (what happens to each incoming request, in order) ----
if (!app.Environment.IsDevelopment())
{
    // Outside Development, show the friendly /Error page instead of a crash screen.
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Errors with no page of their own (404, 503 when the API is down, ...) show Pages/Status.cshtml
// with a plain message instead of a blank browser error page (CAR-60).
app.UseStatusCodePagesWithReExecute("/Status/{0}");

app.UseRouting();

// Reads the sign-in cookie, so pages know who's signed in (CAR-68).
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

// Lets the website tests start the real site (WebApplicationFactory<Program>).
public partial class Program { }
