using CCOS.Shared;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// ---- Services (things the app can use) ----
// Turns the .cshtml files under Pages into web pages.
// Every page under /Officer needs the Officer policy (CAR-69). A visitor who isn't signed in goes to
// Log in ("Log in first"); a signed-in person who isn't an officer sees "Officers only".
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Officer", ClubPolicies.Officer);
});

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
    var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Api:BaseUrl is missing from appsettings.json.");
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Product data comes from CCOS.Api. Run the API alongside the site (see Api:BaseUrl).
// Pages ask for a ProductsApiClient and get one that already knows the API address.
builder.Services.AddHttpClient<ProductsApiClient>(client =>
{
    var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Api:BaseUrl is missing from appsettings.json.");
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<ApiBearerTokenHandler>();

// Event data comes from CCOS.Api too (Events list, event page and Edit Event).
builder.Services.AddHttpClient<EventsApiClient>(client =>
{
    var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Api:BaseUrl is missing from appsettings.json.");
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<ApiBearerTokenHandler>();

// Venue data comes from CCOS.Api too (venue form and the event form's venue dropdown).
builder.Services.AddHttpClient<VenuesApiClient>(client =>
{
    var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Api:BaseUrl is missing from appsettings.json.");
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<ApiBearerTokenHandler>();

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

app.UseRouting();

// Reads the sign-in cookie, so pages know who's signed in (CAR-68).
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
