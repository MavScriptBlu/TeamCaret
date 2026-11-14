using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CCOS.Api.Tests;

/// <summary>
/// Who can do what (CAR-69), through real web requests: a visitor who isn't signed in, a guest
/// (signed in, not a member), a member, an expired member, an officer, an officer whose dues
/// expired, and the advisor, against officer endpoints (the Officer policy) and member pricing
/// (the Member policy).
/// </summary>
public sealed class AccessRulesTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    // A fresh random password each run, so no credential is ever written in the code.
    private static readonly string TestSecret = $"T{Guid.NewGuid():N}!a1";

    private readonly WebApplicationFactory<Program> _baseFactory;
    private WebApplicationFactory<Program> _factory = null!;
    private SqliteConnection _connection = null!;
    private int _activeProductId;

    public AccessRulesTests(WebApplicationFactory<Program> factory)
    {
        _baseFactory = factory;
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        // Test mode: the API skips its own database and sample data and uses this SQLite database.
        _factory = _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Testing", "true");
            builder.ConfigureTestServices(services =>
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection)));
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var shirt = new Product { Category = "Apparel", Name = "Club Shirt", Price = 25m, MemberPrice = 20m, StockQuantity = 10, IsActive = true };
        db.Products.AddRange(
            shirt,
            new Product { Category = "Apparel", Name = "Old Hat", Price = 15m, MemberPrice = 10m, StockQuantity = 5, IsActive = false });
        await db.SaveChangesAsync();
        _activeProductId = shirt.ProductId;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _connection.DisposeAsync();
    }

    public enum Caller
    {
        NotSignedIn,
        Guest,
        Member,
        ExpiredMember,
        Officer,
        ExpiredOfficer,
        Advisor,
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cross-site requests (CAR-83): a sign-in cookie opens nothing
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_officer_signed_in_with_a_cookie_is_refused()
    {
        // The test client keeps cookies like a browser does.
        var client = _factory.CreateClient();
        var email = $"cookie-officer-{Guid.NewGuid():N}@ccos.test";
        var register = await client.PostAsJsonAsync(
            "/api/account/register",
            new RegisterAccountRequest("Test", "Cookie", email, TestSecret));
        register.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.AsNoTracking().SingleAsync(account => account.Email == email);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            db.Members.Add(new Member { MemberId = user.CustomerId, JoinedDate = today.AddMonths(-6), MemberExpiration = today.AddMonths(6) });
            db.Officers.Add(new Officer { OfficerId = user.CustomerId, Title = "President", GrantedDate = today.AddMonths(-6) });
            await db.SaveChangesAsync();
        }

        // Ask for a cookie instead of a token: this is what a cross-site request would ride on.
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = TestSecret });
        login.EnsureSuccessStatusCode();
        Assert.True(login.Headers.Contains("Set-Cookie"), "the login should hand out a cookie for this test to mean anything");

        // Same officer, cookie only, no bearer token: the API treats them as not signed in.
        var response = await client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest("Apparel", "Cookie Mug", "A mug", 12m, 10m, 5));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Officer endpoints
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Caller.NotSignedIn, HttpStatusCode.Unauthorized)]
    [InlineData(Caller.Guest, HttpStatusCode.Forbidden)]
    [InlineData(Caller.Member, HttpStatusCode.Forbidden)]
    [InlineData(Caller.ExpiredMember, HttpStatusCode.Forbidden)]
    [InlineData(Caller.ExpiredOfficer, HttpStatusCode.Forbidden)]
    [InlineData(Caller.Officer, HttpStatusCode.Created)]
    [InlineData(Caller.Advisor, HttpStatusCode.Created)]
    public async Task Creating_a_product_needs_an_officer(Caller caller, HttpStatusCode expected)
    {
        var client = await ClientForAsync(caller);

        var response = await client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest("Apparel", "New Mug", "A mug", 12m, 10m, 5));

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Caller.NotSignedIn, HttpStatusCode.Unauthorized)]
    [InlineData(Caller.Member, HttpStatusCode.Forbidden)]
    [InlineData(Caller.ExpiredOfficer, HttpStatusCode.Forbidden)]
    [InlineData(Caller.Officer, HttpStatusCode.NoContent)]
    [InlineData(Caller.Advisor, HttpStatusCode.NoContent)]
    public async Task Removing_a_product_needs_an_officer(Caller caller, HttpStatusCode expected)
    {
        var client = await ClientForAsync(caller);

        var response = await client.DeleteAsync($"/api/products/{_activeProductId}");

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Caller.NotSignedIn, HttpStatusCode.Unauthorized)]
    [InlineData(Caller.Member, HttpStatusCode.Forbidden)]
    [InlineData(Caller.ExpiredOfficer, HttpStatusCode.Forbidden)]
    [InlineData(Caller.Officer, HttpStatusCode.OK)]
    [InlineData(Caller.Advisor, HttpStatusCode.OK)]
    public async Task The_officer_product_list_needs_an_officer(Caller caller, HttpStatusCode expected)
    {
        var client = await ClientForAsync(caller);

        var response = await client.GetAsync("/api/products?includeInactive=true");

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();
            Assert.Contains(products!, p => p.Name == "Old Hat" && !p.IsActive);
            Assert.All(products!, p => Assert.NotNull(p.MemberPrice));
        }
    }

    [Theory]
    [InlineData(Caller.NotSignedIn, HttpStatusCode.Unauthorized)]
    [InlineData(Caller.Guest, HttpStatusCode.Forbidden)]
    [InlineData(Caller.Member, HttpStatusCode.Forbidden)]
    [InlineData(Caller.Officer, HttpStatusCode.Created)]
    [InlineData(Caller.Advisor, HttpStatusCode.Created)]
    public async Task Adding_a_venue_needs_an_officer(Caller caller, HttpStatusCode expected)
    {
        var client = await ClientForAsync(caller);

        var response = await client.PostAsJsonAsync(
            "/api/venues",
            new CreateVenueRequest("Test Hall", "1 Main St", null, "Wausau", "WI", "54401", 50));

        Assert.Equal(expected, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Member pricing
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Caller.NotSignedIn, false)]
    [InlineData(Caller.Guest, false)]
    [InlineData(Caller.ExpiredMember, false)]
    [InlineData(Caller.ExpiredOfficer, false)]
    [InlineData(Caller.Advisor, false)] // the advisor has no membership
    [InlineData(Caller.Member, true)]
    [InlineData(Caller.Officer, true)]
    public async Task Only_current_members_see_member_prices(Caller caller, bool seesMemberPrice)
    {
        var client = await ClientForAsync(caller);

        var list = await client.GetFromJsonAsync<List<ProductDto>>("/api/products");
        var one = await client.GetFromJsonAsync<ProductDto>($"/api/products/{_activeProductId}");

        var shirt = list!.Single(p => p.ProductId == _activeProductId);
        Assert.Equal(seesMemberPrice ? 20m : null, shirt.MemberPrice);
        Assert.Equal(seesMemberPrice ? 20m : null, one!.MemberPrice);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A client signed in as the given kind of caller. Members and officers get their rows straight
    /// in the database, the way an admin screen will later; the dues date is relative to today.
    /// The advisor gets the Advisor role and no membership.
    /// </summary>
    private async Task<HttpClient> ClientForAsync(Caller caller)
    {
        var client = _factory.CreateClient();
        if (caller == Caller.NotSignedIn)
        {
            return client;
        }

        var email = $"{caller}-{Guid.NewGuid():N}@ccos.test".ToLowerInvariant();
        var register = await client.PostAsJsonAsync(
            "/api/account/register",
            new RegisterAccountRequest("Test", caller.ToString(), email, TestSecret));
        register.EnsureSuccessStatusCode();

        if (caller == Caller.Advisor)
        {
            using var scope = _factory.Services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            if (!await roles.RoleExistsAsync(ClubRoles.Advisor))
            {
                (await roles.CreateAsync(new IdentityRole(ClubRoles.Advisor))).EnsureSucceeded();
            }

            var user = await users.FindByEmailAsync(email);
            (await users.AddToRoleAsync(user!, ClubRoles.Advisor)).EnsureSucceeded();
        }
        else if (caller != Caller.Guest)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.AsNoTracking().SingleAsync(account => account.Email == email);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var expired = caller is Caller.ExpiredMember or Caller.ExpiredOfficer;
            var expiration = expired ? today.AddDays(-30) : today.AddDays(30);

            db.Members.Add(new Member
            {
                MemberId = user.CustomerId,
                JoinedDate = expiration.AddYears(-1),
                MemberExpiration = expiration,
            });

            if (caller is Caller.Officer or Caller.ExpiredOfficer)
            {
                db.Officers.Add(new Officer { OfficerId = user.CustomerId, Title = "President", GrantedDate = expiration.AddYears(-1) });
            }

            await db.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestSecret });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());

        return client;
    }
}

/// <summary>Small helper so a failed Identity call stops the test with its errors.</summary>
internal static class IdentityResultExtensions
{
    public static void EnsureSucceeded(this IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
}
