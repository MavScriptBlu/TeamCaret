using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CCOS.Api.Tests;

/// <summary>
/// RSVP endpoints (CAR-94), through real web requests: RSVP holds a seat, no double booking,
/// sold out, members only, paid events refused, and Cancel RSVP gives the seat back.
/// </summary>
public sealed class RsvpApiTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    // A fresh random password each run, so no credential is ever written in the code.
    private static readonly string TestSecret = $"T{Guid.NewGuid():N}!a1";

    private readonly WebApplicationFactory<Program> _baseFactory;
    private WebApplicationFactory<Program> _factory = null!;
    private SqliteConnection _connection = null!;

    private int _freeEventId;
    private int _fullEventId;
    private int _membersOnlyEventId;
    private int _paidEventId;

    public RsvpApiTests(WebApplicationFactory<Program> factory)
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

        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        Venue venue = new() { Name = "Room 101", Capacity = 100 };
        DateTime nextWeek = DateTime.UtcNow.AddDays(7);
        Event free = NewEvent(venue, "Study Night", nextWeek, capacity: 20);
        Event full = NewEvent(venue, "Tiny Talk", nextWeek, capacity: 1, sold: 1);
        Event membersOnly = NewEvent(venue, "Hackathon", nextWeek, capacity: 30, membersOnly: true);
        Event paid = NewEvent(venue, "CTF Night", nextWeek, capacity: 50);
        db.Events.AddRange(free, full, membersOnly, paid);
        db.TicketTypes.AddRange(
            NewTicket(free, price: 0m, memberPrice: 0m),
            NewTicket(full, price: 0m, memberPrice: 0m),
            NewTicket(membersOnly, price: 0m, memberPrice: 0m),
            NewTicket(paid, price: 10m, memberPrice: 5m));
        await db.SaveChangesAsync();

        _freeEventId = free.EventId;
        _fullEventId = full.EventId;
        _membersOnlyEventId = membersOnly.EventId;
        _paidEventId = paid.EventId;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task RSVP_reserves_one_ticket_and_holds_a_seat()
    {
        HttpClient me = await SignedInClientAsync();

        HttpResponseMessage response = await me.PostAsync($"/api/events/{_freeEventId}/rsvp", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        RsvpStatusDto? status = await response.Content.ReadFromJsonAsync<RsvpStatusDto>();
        Assert.Equal(new RsvpStatusDto(true, 1, true), status);
        Assert.Equal(1, await TicketsSoldAsync(_freeEventId));
    }

    [Fact]
    public async Task RSVPing_twice_does_not_book_a_second_seat()
    {
        HttpClient me = await SignedInClientAsync();

        await me.PostAsync($"/api/events/{_freeEventId}/rsvp", null);
        HttpResponseMessage again = await me.PostAsync($"/api/events/{_freeEventId}/rsvp", null);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(1, await TicketsSoldAsync(_freeEventId));
    }

    [Fact]
    public async Task A_sold_out_event_is_refused()
    {
        HttpClient me = await SignedInClientAsync();

        HttpResponseMessage response = await me.PostAsync($"/api/events/{_fullEventId}/rsvp", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await TicketsSoldAsync(_fullEventId));
    }

    [Fact]
    public async Task Members_only_is_refused_for_someone_who_is_not_a_member()
    {
        HttpClient me = await SignedInClientAsync();

        HttpResponseMessage response = await me.PostAsync($"/api/events/{_membersOnlyEventId}/rsvp", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await TicketsSoldAsync(_membersOnlyEventId));
    }

    [Fact]
    public async Task A_paid_event_is_not_an_RSVP()
    {
        HttpClient me = await SignedInClientAsync();

        HttpResponseMessage response = await me.PostAsync($"/api/events/{_paidEventId}/rsvp", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Not free", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Cancel_RSVP_gives_the_seat_back_and_cancels_the_order()
    {
        HttpClient me = await SignedInClientAsync();
        await me.PostAsync($"/api/events/{_freeEventId}/rsvp", null);

        HttpResponseMessage cancel = await me.DeleteAsync($"/api/events/{_freeEventId}/rsvp");

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(0, await TicketsSoldAsync(_freeEventId));
        RsvpStatusDto? status = await me.GetFromJsonAsync<RsvpStatusDto>($"/api/events/{_freeEventId}/rsvp");
        Assert.Equal(new RsvpStatusDto(false, 0, false), status);

        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Order order = await db.Orders.AsNoTracking().SingleAsync();
        Assert.Equal("Canceled", order.Status);
        TicketRegistration registration = await db.TicketRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal(RegistrationStatus.Canceled, registration.Status);
    }

    [Fact]
    public async Task Cancel_with_nothing_to_cancel_is_404()
    {
        HttpClient me = await SignedInClientAsync();

        HttpResponseMessage cancel = await me.DeleteAsync($"/api/events/{_freeEventId}/rsvp");

        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
    }

    [Fact]
    public async Task Signed_out_gets_401()
    {
        HttpResponseMessage response = await _factory.CreateClient().PostAsync($"/api/events/{_freeEventId}/rsvp", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<int> TicketsSoldAsync(int eventId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Events.AsNoTracking().SingleAsync(e => e.EventId == eventId)).TicketsSold;
    }

    // Registers a new account, logs in and returns a client that sends the bearer token.
    private async Task<HttpClient> SignedInClientAsync()
    {
        HttpClient client = _factory.CreateClient();
        string email = $"guest-{Guid.NewGuid():N}@ccos.test";

        HttpResponseMessage register = await client.PostAsJsonAsync(
            "/api/account/register",
            new RegisterAccountRequest("Test", "Guest", email, TestSecret));
        register.EnsureSuccessStatusCode();

        HttpResponseMessage login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestSecret });
        login.EnsureSuccessStatusCode();
        JsonElement body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static Event NewEvent(Venue venue, string name, DateTime startsAtUtc, int capacity, int sold = 0, bool membersOnly = false) => new()
    {
        Venue = venue,
        Name = name,
        StartsAtUtc = startsAtUtc,
        EndsAtUtc = startsAtUtc.AddHours(2),
        TicketCapacity = capacity,
        TicketsSold = sold,
        MembersOnly = membersOnly,
    };

    private static TicketType NewTicket(Event @event, decimal price, decimal memberPrice) => new()
    {
        Event = @event,
        Category = "Tickets",
        Name = "General",
        Price = price,
        MemberPrice = memberPrice,
        StockQuantity = 0,
    };
}
