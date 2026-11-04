using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Repositories;
using CCOS.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CCOS.Api.Tests;

/// <summary>
/// Tests for sign-in (CAR-67): registering, logging in, GET /api/me, and the Member and Officer
/// access rules. Runs the real API in memory against a throwaway SQLite database, in test mode.
/// </summary>
public sealed class AccountApiTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    // A fresh random password each run, so no credential is ever written in the code.
    // It meets Identity's rules: upper and lower case letters, a digit and a symbol.
    private static readonly string TestSecret = $"T{Guid.NewGuid():N}!a1";

    private readonly WebApplicationFactory<Program> _baseFactory;
    private WebApplicationFactory<Program> _factory = null!;
    private SqliteConnection _connection = null!;

    public AccountApiTests(WebApplicationFactory<Program> factory)
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
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Register, login and /api/me
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registering creates the Customer and the login together; the new person can sign in, and
    /// /api/me shows them as a guest (not a member, not an officer).
    /// </summary>
    [Fact]
    public async Task Register_then_login_shows_a_guest_on_me()
    {
        var client = _factory.CreateClient();

        var register = await RegisterAsync(client, "new.person@ccos.test", "New", "Person");
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        await SignInAsync(client, "new.person@ccos.test");
        var me = await client.GetFromJsonAsync<CurrentAccountDto>("/api/me");

        Assert.NotNull(me);
        Assert.Equal("New", me.FirstName);
        Assert.Equal("Person", me.LastName);
        Assert.Equal("new.person@ccos.test", me.Email);
        Assert.Null(me.MemberStatus);
        Assert.False(me.IsCurrentMember);
        Assert.False(me.IsCurrentOfficer);

        // The login points at the customer that was created with it.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(account => account.Email == "new.person@ccos.test");
        Assert.Equal(me.CustomerId, user.CustomerId);
    }

    /// <summary>Nobody signed in: /api/me answers 401 (not a redirect to a login page).</summary>
    [Fact]
    public async Task Me_without_signing_in_returns_401()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A second account with the same email is refused with the message on Email, and the
    /// transaction rolls back, so no extra Customer row is left behind.
    /// </summary>
    [Fact]
    public async Task Register_with_a_used_email_returns_400_and_leaves_no_customer()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await RegisterAsync(client, "twice@ccos.test", "First", "Try")).StatusCode);

        var second = await RegisterAsync(client, "twice@ccos.test", "Second", "Try");

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("Email", out _));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Customers.CountAsync());
    }

    /// <summary>
    /// Identity's built-in /api/auth/register is turned off, because it would create a login
    /// without a Customer.
    /// </summary>
    [Fact]
    public async Task Builtin_register_is_turned_off()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = "skip@ccos.test", password = TestSecret });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync());
    }

    /// <summary>A wrong password doesn't sign in.</summary>
    [Fact]
    public async Task Login_with_a_wrong_password_returns_401()
    {
        var client = _factory.CreateClient();
        await RegisterAsync(client, "wrong.guess@ccos.test", "Wrong", "Guess");

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "wrong.guess@ccos.test", password = TestSecret + "x" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Member and Officer access rules
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The Member and Officer policies follow CAR-28: a guest passes neither; a current member
    /// passes Member only; an expired member passes neither; a current officer passes both; an
    /// officer whose dues expired passes neither (their title is kept, access pauses).
    /// </summary>
    [Fact]
    public async Task Member_and_Officer_policies_follow_the_membership_rules()
    {
        var client = _factory.CreateClient();
        var today = ClubClock.ToClubDate(DateTime.UtcNow);

        var guest = await CreateAccountAsync(client, "guest@ccos.test");
        var member = await CreateAccountAsync(client, "member@ccos.test", expiration: today.AddMonths(6));
        var expiredMember = await CreateAccountAsync(client, "expired@ccos.test", expiration: today.AddDays(-1));
        var officer = await CreateAccountAsync(client, "officer@ccos.test", expiration: today.AddMonths(6), title: "President");
        var expiredOfficer = await CreateAccountAsync(client, "old.officer@ccos.test", expiration: today.AddDays(-1), title: "Treasurer");

        Assert.False(await PassesAsync(guest, ClubPolicies.Member));
        Assert.False(await PassesAsync(guest, ClubPolicies.Officer));

        Assert.True(await PassesAsync(member, ClubPolicies.Member));
        Assert.False(await PassesAsync(member, ClubPolicies.Officer));

        Assert.False(await PassesAsync(expiredMember, ClubPolicies.Member));
        Assert.False(await PassesAsync(expiredMember, ClubPolicies.Officer));

        Assert.True(await PassesAsync(officer, ClubPolicies.Member));
        Assert.True(await PassesAsync(officer, ClubPolicies.Officer));

        Assert.False(await PassesAsync(expiredOfficer, ClubPolicies.Member));
        Assert.False(await PassesAsync(expiredOfficer, ClubPolicies.Officer));
    }

    /// <summary>
    /// The club's advisor (Advisor role) passes the Officer policy without a membership or a title,
    /// /api/me says so, and taking the role away counts on the next check (CAR-67, Remaining Work checklist).
    /// </summary>
    [Fact]
    public async Task Advisor_passes_Officer_without_a_membership()
    {
        var client = _factory.CreateClient();
        var advisor = await CreateAccountAsync(client, "advisor@ccos.test");
        await SetAdvisorRoleAsync(advisor, isAdvisor: true);

        Assert.True(await PassesAsync(advisor, ClubPolicies.Officer));
        Assert.False(await PassesAsync(advisor, ClubPolicies.Member));

        await SignInAsync(client, "advisor@ccos.test");
        var me = await client.GetFromJsonAsync<CurrentAccountDto>("/api/me");
        Assert.NotNull(me);
        Assert.True(me.IsAdvisor);
        Assert.False(me.IsCurrentMember);
        Assert.False(me.IsCurrentOfficer);

        // Role removed: no longer passes Officer, with no new sign-in needed.
        await SetAdvisorRoleAsync(advisor, isAdvisor: false);
        Assert.False(await PassesAsync(advisor, ClubPolicies.Officer));
    }

    /// <summary>/api/me for a current officer shows them as a current member and officer, with their title.</summary>
    [Fact]
    public async Task Me_shows_a_current_officer_with_their_title()
    {
        var client = _factory.CreateClient();
        var today = ClubClock.ToClubDate(DateTime.UtcNow);
        await CreateAccountAsync(client, "president@ccos.test", expiration: today.AddMonths(6), title: "President");

        await SignInAsync(client, "president@ccos.test");
        var me = await client.GetFromJsonAsync<CurrentAccountDto>("/api/me");

        Assert.NotNull(me);
        Assert.Equal(MemberStatus.Active, me.MemberStatus);
        Assert.True(me.IsCurrentMember);
        Assert.True(me.IsCurrentOfficer);
        Assert.Equal("President", me.OfficerTitle);
    }

    /// <summary>
    /// Membership isn't cached in the token: when a signed-in member's row is marked inactive,
    /// the very next /api/me shows it.
    /// </summary>
    [Fact]
    public async Task Membership_changes_show_up_on_the_next_request()
    {
        var client = _factory.CreateClient();
        var today = ClubClock.ToClubDate(DateTime.UtcNow);
        var account = await CreateAccountAsync(client, "leaving@ccos.test", expiration: today.AddMonths(6));

        await SignInAsync(client, "leaving@ccos.test");
        Assert.True((await client.GetFromJsonAsync<CurrentAccountDto>("/api/me"))!.IsCurrentMember);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.Members.SingleAsync(memberRow => memberRow.MemberId == account.CustomerId);
            row.IsActive = false;
            row.DeactivatedDate = today;
            await db.SaveChangesAsync();
        }

        var after = await client.GetFromJsonAsync<CurrentAccountDto>("/api/me");
        Assert.False(after!.IsCurrentMember);
        Assert.Equal(MemberStatus.Inactive, after.MemberStatus);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>The login's user id and the CustomerId behind it.</summary>
    private sealed record TestAccount(string UserId, int CustomerId);

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, string firstName, string lastName) =>
        client.PostAsJsonAsync("/api/account/register", new RegisterAccountRequest(firstName, lastName, email, TestSecret));

    /// <summary>Signs in through Identity's login endpoint and puts the token on the client.</summary>
    private static async Task SignInAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestSecret });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = body.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    /// <summary>
    /// Registers an account through the API, then (optionally) gives it a membership and an
    /// officer title directly in the database, the way an admin screen will later.
    /// </summary>
    private async Task<TestAccount> CreateAccountAsync(HttpClient client, string email, DateOnly? expiration = null, string? title = null)
    {
        (await RegisterAsync(client, email, "Test", email)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(account => account.Email == email);

        if (expiration is not null)
        {
            db.Members.Add(new Member
            {
                MemberId = user.CustomerId,
                JoinedDate = expiration.Value.AddYears(-1),
                MemberExpiration = expiration.Value,
            });

            if (title is not null)
            {
                db.Officers.Add(new Officer { OfficerId = user.CustomerId, Title = title, GrantedDate = expiration.Value.AddYears(-1) });
            }

            await db.SaveChangesAsync();
        }

        return new TestAccount(user.Id, user.CustomerId);
    }

    /// <summary>Gives an account the Advisor role (creating the role the first time), or takes it away.</summary>
    private async Task SetAdvisorRoleAsync(TestAccount account, bool isAdvisor)
    {
        using var scope = _factory.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByIdAsync(account.UserId);
        Assert.NotNull(user);

        if (!await roles.RoleExistsAsync(ClubRoles.Advisor))
        {
            Assert.True((await roles.CreateAsync(new IdentityRole(ClubRoles.Advisor))).Succeeded);
        }

        var result = isAdvisor
            ? await users.AddToRoleAsync(user, ClubRoles.Advisor)
            : await users.RemoveFromRoleAsync(user, ClubRoles.Advisor);
        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Runs one policy for a signed-in account, through the same authorization service the API uses.
    /// </summary>
    private async Task<bool> PassesAsync(TestAccount account, string policy)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, account.UserId)],
            authenticationType: "Test"));

        using var scope = _factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(principal, policy);
        return result.Succeeded;
    }
}
