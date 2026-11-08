using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CCOS.Api.Tests;

/// <summary>
/// Tests for the event create, edit, remove and officer list endpoints (CAR-55, CAR-56).
/// Uses an in-memory SQLite database. The main client is signed in as an officer, since these
/// endpoints are officer-only (CAR-69); the anonymous client checks what everyone else gets.
/// </summary>
public class EventsWriteControllerTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private SqliteConnection _connection = null!;
    private HttpClient _client = null!;
    private HttpClient _anonymous = null!;
    private int _pastEventId;

    private int _venueId;
    private int _removedVenueId;
    private int _soldEventId;
    private int _removedEventId;

    public EventsWriteControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var customizedFactory = _factory.WithWebHostBuilder(builder =>
        {
            // Test mode: skip the API's own database setup and sample data (see CAR-72).
            builder.UseSetting("Testing", "true");
            builder.ConfigureTestServices(services =>
            {
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            });
        });

        _client = customizedFactory.CreateClient();
        _anonymous = customizedFactory.CreateClient();

        using var scope = customizedFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var venue = new Venue { Name = "Main Hall", Capacity = 100 };
        var removedVenue = new Venue { Name = "Old Gym", Capacity = 50, IsActive = false };

        var soldEvent = new Event
        {
            Venue = venue,
            Name = "Sold Out Soon",
            StartsAtUtc = DateTime.UtcNow.AddDays(10),
            EndsAtUtc = DateTime.UtcNow.AddDays(10).AddHours(2),
            TicketCapacity = 20,
            TicketsSold = 12,
        };
        soldEvent.TicketTypes.Add(new TicketType { Category = "Tickets", Name = "General", Price = 10m, MemberPrice = 5m });

        // A removed event, and one in the past: both only show up on the officer list.
        var removedEvent = new Event
        {
            Venue = venue,
            Name = "Removed Meetup",
            StartsAtUtc = DateTime.UtcNow.AddDays(-30),
            EndsAtUtc = DateTime.UtcNow.AddDays(-30).AddHours(2),
            TicketCapacity = 30,
            IsActive = false,
        };
        removedEvent.TicketTypes.Add(new TicketType { Category = "Tickets", Name = "General", Price = 0m, MemberPrice = 0m });

        // Active, but already over: hidden from the public list and its page (CAR-69 review).
        var pastEvent = new Event
        {
            Venue = venue,
            Name = "Last Month's Meetup",
            StartsAtUtc = DateTime.UtcNow.AddDays(-20),
            EndsAtUtc = DateTime.UtcNow.AddDays(-20).AddHours(2),
            TicketCapacity = 30,
        };
        pastEvent.TicketTypes.Add(new TicketType { Category = "Tickets", Name = "General", Price = 3m, MemberPrice = 1m });

        db.AddRange(venue, removedVenue, soldEvent, removedEvent, pastEvent);
        await db.SaveChangesAsync();
        _pastEventId = pastEvent.EventId;

        _venueId = venue.VenueId;
        _removedVenueId = removedVenue.VenueId;
        _soldEventId = soldEvent.EventId;
        _removedEventId = removedEvent.EventId;

        await TestAccounts.SignInAsOfficerAsync(customizedFactory, _client);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _anonymous.Dispose();
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }

    // A request that passes every rule, so each test only changes the one thing it checks.
    private CreateEventRequest ValidCreate(string name = "Robot Night", int? venueId = null) => new(
        name,
        "Bring a robot.",
        new DateTime(2027, 3, 4, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2027, 3, 4, 2, 0, 0, DateTimeKind.Utc),
        venueId ?? _venueId,
        40,
        false,
        10m,
        5m);

    private UpdateEventRequest ValidUpdate(
        string name = "Sold Out Soon",
        int? venueId = null,
        int capacity = 20,
        bool isActive = true) => new(
        name,
        null,
        DateTime.UtcNow.AddDays(10),
        DateTime.UtcNow.AddDays(10).AddHours(2),
        venueId ?? _venueId,
        capacity,
        false,
        12m,
        6m,
        isActive);

    // ---- POST /api/events ----

    [Fact]
    public async Task Post_creates_the_event_with_a_General_ticket_type()
    {
        var response = await _client.PostAsJsonAsync("/api/events", ValidCreate());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.NotNull(created);
        Assert.Equal("Robot Night", created.Name);
        Assert.Equal("Main Hall", created.VenueName);
        Assert.True(created.IsActive);
        Assert.Equal(new DateTime(2027, 3, 4, 0, 0, 0, DateTimeKind.Utc), created.StartsAtUtc);

        var ticketType = Assert.Single(created.TicketTypes);
        Assert.Equal("General", ticketType.Name);
        Assert.Equal(10m, ticketType.Price);
        Assert.Equal(5m, ticketType.MemberPrice);
    }

    [Fact]
    public async Task Post_makes_the_new_event_show_up_in_the_public_list_once_it_is_upcoming()
    {
        var future = ValidCreate("Future Fun") with
        {
            StartsAtUtc = DateTime.UtcNow.AddDays(5),
            EndsAtUtc = DateTime.UtcNow.AddDays(5).AddHours(1),
        };
        await _client.PostAsJsonAsync("/api/events", future);

        var list = await _client.GetFromJsonAsync<List<EventDto>>("/api/events");

        Assert.NotNull(list);
        Assert.Contains(list, item => item.Name == "Future Fun");
    }

    [Fact]
    public async Task Post_rejects_an_event_that_ends_before_it_starts()
    {
        var bad = ValidCreate() with { EndsAtUtc = new DateTime(2027, 3, 3, 0, 0, 0, DateTimeKind.Utc) };

        var response = await _client.PostAsJsonAsync("/api/events", bad);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("EndsAtUtc", body);
    }

    [Fact]
    public async Task Post_rejects_a_member_price_above_the_regular_price()
    {
        var bad = ValidCreate() with { TicketPrice = 5m, MemberTicketPrice = 6m };

        var response = await _client.PostAsJsonAsync("/api/events", bad);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("MemberTicketPrice", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Post_rejects_a_blank_name(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/events", ValidCreate(name));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_rejects_a_capacity_under_one()
    {
        var response = await _client.PostAsJsonAsync("/api/events", ValidCreate() with { TicketCapacity = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_rejects_a_venue_that_does_not_exist()
    {
        var response = await _client.PostAsJsonAsync("/api/events", ValidCreate(venueId: 99999));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("VenueId", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Post_rejects_a_removed_venue()
    {
        var response = await _client.PostAsJsonAsync("/api/events", ValidCreate(venueId: _removedVenueId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- PUT /api/events/{id} ----

    [Fact]
    public async Task Put_saves_the_changes_and_the_ticket_prices()
    {
        var response = await _client.PutAsJsonAsync($"/api/events/{_soldEventId}", ValidUpdate(name: "Renamed Night"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.NotNull(updated);
        Assert.Equal("Renamed Night", updated.Name);
        var ticketType = Assert.Single(updated.TicketTypes);
        Assert.Equal(12m, ticketType.Price);
        Assert.Equal(6m, ticketType.MemberPrice);

        // It really was saved, not just echoed back.
        var reloaded = await _client.GetFromJsonAsync<EventDto>($"/api/events/{_soldEventId}");
        Assert.Equal("Renamed Night", reloaded!.Name);
    }

    [Fact]
    public async Task Put_returns_404_for_an_unknown_event()
    {
        var response = await _client.PutAsJsonAsync("/api/events/99999", ValidUpdate());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_refuses_a_capacity_below_the_tickets_already_sold()
    {
        var response = await _client.PutAsJsonAsync($"/api/events/{_soldEventId}", ValidUpdate(capacity: 5));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("TicketCapacity", body);
        Assert.Contains("12", body);
    }

    [Fact]
    public async Task Put_allows_a_capacity_equal_to_the_tickets_sold()
    {
        var response = await _client.PutAsJsonAsync($"/api/events/{_soldEventId}", ValidUpdate(capacity: 12));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Put_refuses_a_removed_venue_when_the_event_is_moving_to_it()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/events/{_soldEventId}",
            ValidUpdate(venueId: _removedVenueId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_with_IsActive_true_restores_a_removed_event()
    {
        var restore = new UpdateEventRequest(
            "Removed Meetup",
            null,
            DateTime.UtcNow.AddDays(3),
            DateTime.UtcNow.AddDays(3).AddHours(2),
            _venueId,
            30,
            false,
            0m,
            0m,
            IsActive: true);

        var response = await _client.PutAsJsonAsync($"/api/events/{_removedEventId}", restore);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var publicEvent = await _client.GetAsync($"/api/events/{_removedEventId}");
        Assert.Equal(HttpStatusCode.OK, publicEvent.StatusCode);
    }

    // ---- Who can do what (CAR-69) ----

    [Fact]
    public async Task Officer_endpoints_need_a_signed_in_officer()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.PostAsJsonAsync("/api/events", ValidCreate())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.PutAsJsonAsync($"/api/events/{_soldEventId}", ValidUpdate(name: "Nope"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.DeleteAsync($"/api/events/{_soldEventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.GetAsync("/api/events/officer")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.GetAsync($"/api/events/officer/{_soldEventId}")).StatusCode);
    }

    [Fact]
    public async Task Visitors_get_no_ticket_member_prices_but_officers_do()
    {
        var publicEvent = await _anonymous.GetFromJsonAsync<EventDto>($"/api/events/{_soldEventId}");
        var publicList = await _anonymous.GetFromJsonAsync<List<EventDto>>("/api/events");
        var officerEvent = await _client.GetFromJsonAsync<EventDto>($"/api/events/{_soldEventId}");

        Assert.Null(Assert.Single(publicEvent!.TicketTypes).MemberPrice);
        Assert.All(publicList!.SelectMany(e => e.TicketTypes), t => Assert.Null(t.MemberPrice));
        Assert.Equal(5m, Assert.Single(officerEvent!.TicketTypes).MemberPrice);
    }

    [Fact]
    public async Task A_past_event_cannot_be_opened_by_its_link()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _anonymous.GetAsync($"/api/events/{_pastEventId}")).StatusCode);

        // Officers still reach it through the officer endpoint.
        var officerCopy = await _client.GetFromJsonAsync<EventDto>($"/api/events/officer/{_pastEventId}");
        Assert.Equal("Last Month's Meetup", officerCopy!.Name);
    }

    // ---- DELETE /api/events/{id} ----

    [Fact]
    public async Task Delete_removes_the_event_from_the_public_pages_but_keeps_the_row()
    {
        var response = await _client.DeleteAsync($"/api/events/{_soldEventId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/events/{_soldEventId}")).StatusCode);

        var officerCopy = await _client.GetFromJsonAsync<EventDto>($"/api/events/officer/{_soldEventId}");
        Assert.NotNull(officerCopy);
        Assert.False(officerCopy.IsActive);
    }

    [Fact]
    public async Task Delete_twice_still_returns_204()
    {
        await _client.DeleteAsync($"/api/events/{_soldEventId}");

        var second = await _client.DeleteAsync($"/api/events/{_soldEventId}");

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task Delete_returns_404_for_an_unknown_event()
    {
        var response = await _client.DeleteAsync("/api/events/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- GET officer endpoints ----

    [Fact]
    public async Task Officer_list_includes_removed_and_past_events()
    {
        var list = await _client.GetFromJsonAsync<List<EventDto>>("/api/events/officer");

        Assert.NotNull(list);
        Assert.Contains(list, item => item.EventId == _removedEventId && !item.IsActive);
        Assert.Contains(list, item => item.EventId == _soldEventId);
    }

    [Fact]
    public async Task Officer_get_returns_404_for_an_unknown_event()
    {
        var response = await _client.GetAsync("/api/events/officer/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- the rules on their own ----

    [Fact]
    public void EventRules_accepts_a_valid_event()
    {
        var problems = EventRules.Check(DateTime.UtcNow, DateTime.UtcNow.AddHours(1), 10m, 10m);

        Assert.Empty(problems);
    }

    [Fact]
    public void EventRules_flags_an_end_that_equals_the_start()
    {
        var start = DateTime.UtcNow;

        var problems = EventRules.Check(start, start, 10m, 5m).ToList();

        var problem = Assert.Single(problems);
        Assert.Contains("EndsAtUtc", problem.MemberNames);
    }

    [Fact]
    public void EventRules_flags_both_problems_at_once()
    {
        var start = DateTime.UtcNow;

        List<ValidationResult> problems = EventRules.Check(start, start.AddHours(-1), 1m, 2m).ToList();

        Assert.Equal(2, problems.Count);
    }
}
