using CCOS.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Services (things the app can use) ----
// Turns the .cshtml files under Pages into web pages.
builder.Services.AddRazorPages();

// Product data comes from CCOS.Api. Run the API alongside the site (see Api:BaseUrl).
// Pages ask for a ProductsApiClient and get one that already knows the API address.
builder.Services.AddHttpClient<ProductsApiClient>(client =>
{
    var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Api:BaseUrl is missing from appsettings.json.");
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

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

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
