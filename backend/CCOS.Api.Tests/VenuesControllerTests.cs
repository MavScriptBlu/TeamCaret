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
/// Tests for the venue endpoints: single venue GET, officer list, PUT, DELETE (CAR-57, CAR-58)
/// and the ZIP code rule on create and update (CAR-63). Uses an in-memory SQLite database.
/// </summary>
public class VenuesControllerTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private WebApplicationFactory<Program> _testFactory = null!;
    private SqliteConnection _connection = null!;
    private HttpClient _client = null!;

    private int _mainVenueId;
    private int _removedVenueId;
    private int _noAddressVenueId;

    public VenuesControllerTests(WebApplicationFactory<Program> factory)
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

        _testFactory = customizedFactory;
        _client = customizedFactory.CreateClient();

        using var scope = customizedFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var mainVenue = new Venue
        {
            Name = "Main Hall",
            Capacity = 100,
            Address = new VenueAddress
            {
                AddressLine1 = "1001 Centerpoint Dr",
                AddressLine2 = "Room 101",
                City = "Stevens Point",
                State = "WI",
                ZipCode = "54481",
            },
        };

        var removedVenue = new Venue
        {
            Name = "Old Gym",
            Capacity = 50,
            IsActive = false,
            Address = new VenueAddress
            {
                AddressLine1 = "5 Old Rd",
                City = "Wisconsin Rapids",
                State = "WI",
                ZipCode = "54494-1234",
            },
        };

        // Like the sample venues: saved without an address.
        var noAddressVenue = new Venue { Name = "Sample Room", Capacity = 30 };

        db.Venues.AddRange(mainVenue, removedVenue, noAddressVenue);
        await db.SaveChangesAsync();

        _mainVenueId = mainVenue.VenueId;
        _removedVenueId = removedVenue.VenueId;
        _noAddressVenueId = noAddressVenue.VenueId;

        // Adding a venue is officer-only since CAR-69, so the ZIP tests sign in as an officer.
        await TestAccounts.SignInAsOfficerAsync(customizedFactory, _client);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }

    // A request that passes every rule, so each test only changes the one thing it checks.
    private static CreateVenueRequest ValidCreate(string name = "New Venue", string zipCode = "54481") =>
        new(name, "1 Main St", null, "Stevens Point", "WI", zipCode, 40);

    // Adds an event at a venue, starting this many days from now (negative is in the past).
    private async Task<int> AddEventAsync(int venueId, string name, int daysFromNow, bool isActive = true)
    {
        using var scope = _testFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var startsAtUtc = DateTime.UtcNow.AddDays(daysFromNow);
        var newEvent = new Event
        {
            VenueId = venueId,
            Name = name,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = startsAtUtc.AddHours(2),
            TicketCapacity = 30,
            IsActive = isActive,
        };
        newEvent.TicketTypes.Add(new TicketType { Category = "Tickets", Name = "General", Price = 5m, MemberPrice = 0m });

        db.Events.Add(newEvent);
        await db.SaveChangesAsync();
        return newEvent.EventId;
    }

    private static UpdateVenueRequest ValidUpdate(string name = "Main Hall", string zipCode = "54481", bool isActive = true) =>
        new(name, "1001 Centerpoint Dr", "Room 101", "Stevens Point", "WI", zipCode, 100, isActive);

    // ---- GET /api/venues/{id} ----

    [Fact]
    public async Task Get_returns_the_venue_with_its_address()
    {
        var venue = await _client.GetFromJsonAsync<VenueDetailDto>($"/api/venues/{_mainVenueId}");

        Assert.NotNull(venue);
        Assert.Equal("Main Hall", venue.Name);
        Assert.Equal("1001 Centerpoint Dr", venue.AddressLine1);
        Assert.Equal("Room 101", venue.AddressLine2);
        Assert.Equal("54481", venue.ZipCode);
        Assert.True(venue.IsActive);
    }

    [Fact]
    public async Task Get_returns_a_removed_venue_so_it_can_be_restored()
    {
        var venue = await _client.GetFromJsonAsync<VenueDetailDto>($"/api/venues/{_removedVenueId}");

        Assert.NotNull(venue);
        Assert.False(venue.IsActive);
    }

    [Fact]
    public async Task Get_returns_empty_address_parts_for_a_venue_without_an_address()
    {
        var venue = await _client.GetFromJsonAsync<VenueDetailDto>($"/api/venues/{_noAddressVenueId}");

        Assert.NotNull(venue);
        Assert.Null(venue.AddressLine1);
        Assert.Null(venue.ZipCode);
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_venue()
    {
        var response = await _client.GetAsync("/api/venues/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- GET /api/venues/officer ----

    [Fact]
    public async Task OfficerList_includes_removed_venues_by_name()
    {
        var venues = await _client.GetFromJsonAsync<List<VenueDetailDto>>("/api/venues/officer");

        Assert.NotNull(venues);
        Assert.Equal(["Main Hall", "Old Gym", "Sample Room"], venues.Select(venue => venue.Name));
        Assert.Contains(venues, venue => !venue.IsActive);
    }

    [Fact]
    public async Task PublicList_still_hides_removed_venues()
    {
        var venues = await _client.GetFromJsonAsync<List<VenueDto>>("/api/venues");

        Assert.NotNull(venues);
        Assert.DoesNotContain(venues, venue => venue.Name == "Old Gym");
    }

    // ---- PUT /api/venues/{id} ----

    [Fact]
    public async Task Put_updates_the_venue_and_address()
    {
        var request = new UpdateVenueRequest("Main Hall East", "2 New St", null, "Plover", "wi", "54467-0001", 120);

        var response = await _client.PutAsJsonAsync($"/api/venues/{_mainVenueId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var venue = await _client.GetFromJsonAsync<VenueDetailDto>($"/api/venues/{_mainVenueId}");
        Assert.NotNull(venue);
        Assert.Equal("Main Hall East", venue.Name);
        Assert.Equal("2 New St", venue.AddressLine1);
        Assert.Null(venue.AddressLine2);
        Assert.Equal("WI", venue.State);
        Assert.Equal("54467-0001", venue.ZipCode);
        Assert.Equal(120, venue.Capacity);
    }

    [Fact]
    public async Task Put_gives_a_venue_without_an_address_its_first_address()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/venues/{_noAddressVenueId}",
            ValidUpdate("Sample Room"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var venue = await _client.GetFromJsonAsync<VenueDetailDto>($"/api/venues/{_noAddressVenueId}");
        Assert.NotNull(venue);
        Assert.Equal("1001 Centerpoint Dr", venue.AddressLine1);
    }

    [Fact]
    public async Task Put_with_IsActive_true_restores_a_removed_venue()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/venues/{_removedVenueId}",
            ValidUpdate("Old Gym", isActive: true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var venues = await _client.GetFromJsonAsync<List<VenueDto>>("/api/venues");
        Assert.Contains(venues!, venue => venue.Name == "Old Gym");
    }

    [Fact]
    public async Task Put_allows_the_venue_to_keep_its_own_name()
    {
        var response = await _client.PutAsJsonAsync($"/api/venues/{_mainVenueId}", ValidUpdate("main hall"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Put_rejects_a_name_another_venue_already_uses()
    {
        var response = await _client.PutAsJsonAsync($"/api/venues/{_mainVenueId}", ValidUpdate("old gym"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("already exists", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Put_returns_404_for_an_unknown_venue()
    {
        var response = await _client.PutAsJsonAsync("/api/venues/9999", ValidUpdate("Nowhere Hall"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Upcoming events for the "Remove venue?" warning (CAR-79) ----

    [Fact]
    public async Task OfficerList_lists_upcoming_events_soonest_first_and_skips_past_and_removed_ones()
    {
        await AddEventAsync(_mainVenueId, "Later Workshop", daysFromNow: 14);
        await AddEventAsync(_mainVenueId, "Next Week Meetup", daysFromNow: 7);
        await AddEventAsync(_mainVenueId, "Last Month's Meetup", daysFromNow: -30);
        await AddEventAsync(_mainVenueId, "Removed Meetup", daysFromNow: 10, isActive: false);

        var list = await _client.GetFromJsonAsync<List<VenueDetailDto>>("/api/venues/officer");
        var mainHall = Assert.Single(list!, venue => venue.VenueId == _mainVenueId);

        string[] soonestFirst = ["Next Week Meetup", "Later Workshop"];
        Assert.NotNull(mainHall.UpcomingEvents);
        Assert.Equal(soonestFirst, mainHall.UpcomingEvents.Select(@event => @event.Name));
    }

    [Fact]
    public async Task OfficerList_venue_with_only_past_events_has_no_upcoming_events()
    {
        await AddEventAsync(_noAddressVenueId, "Last Month's Meetup", daysFromNow: -30);

        var list = await _client.GetFromJsonAsync<List<VenueDetailDto>>("/api/venues/officer");
        var venue = Assert.Single(list!, venue => venue.VenueId == _noAddressVenueId);

        Assert.NotNull(venue.UpcomingEvents);
        Assert.Empty(venue.UpcomingEvents);
    }

    [Fact]
    public async Task Get_includes_the_venues_upcoming_events()
    {
        await AddEventAsync(_mainVenueId, "Next Week Meetup", daysFromNow: 7);

        var venue = await _client.GetFromJsonAsync<VenueDetailDto>($"/api/venues/{_mainVenueId}");

        Assert.NotNull(venue?.UpcomingEvents);
        Assert.Equal("Next Week Meetup", Assert.Single(venue.UpcomingEvents).Name);
    }

    [Fact]
    public async Task Get_venue_with_no_events_has_no_upcoming_events()
    {
        var venue = await _client.GetFromJsonAsync<VenueDetailDto>($"/api/venues/{_mainVenueId}");

        Assert.NotNull(venue?.UpcomingEvents);
        Assert.Empty(venue.UpcomingEvents);
    }

    [Fact]
    public async Task Delete_still_removes_a_venue_with_upcoming_events_and_the_events_keep_it()
    {
        // CAR-79 decision: warn, don't block.
        var eventId = await AddEventAsync(_mainVenueId, "Next Week Meetup", daysFromNow: 7);

        var response = await _client.DeleteAsync($"/api/venues/{_mainVenueId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var upcomingEvent = await _client.GetFromJsonAsync<EventDto>($"/api/events/{eventId}");
        Assert.NotNull(upcomingEvent);
        Assert.Equal(_mainVenueId, upcomingEvent.VenueId);
        Assert.Equal("Main Hall", upcomingEvent.VenueName);
    }

    // ---- DELETE /api/venues/{id} ----

    [Fact]
    public async Task Delete_removes_the_venue_but_keeps_the_row()
    {
        var response = await _client.DeleteAsync($"/api/venues/{_mainVenueId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Gone from the public dropdown list...
        var publicList = await _client.GetFromJsonAsync<List<VenueDto>>("/api/venues");
        Assert.DoesNotContain(publicList!, venue => venue.Name == "Main Hall");

        // ...but still there for officers, marked as removed.
        var venue = await _client.GetFromJsonAsync<VenueDetailDto>($"/api/venues/{_mainVenueId}");
        Assert.NotNull(venue);
        Assert.False(venue.IsActive);
    }

    [Fact]
    public async Task Delete_twice_still_succeeds()
    {
        await _client.DeleteAsync($"/api/venues/{_mainVenueId}");

        var response = await _client.DeleteAsync($"/api/venues/{_mainVenueId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_returns_404_for_an_unknown_venue()
    {
        var response = await _client.DeleteAsync("/api/venues/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- ZIP code rule (CAR-63), through the API ----

    [Theory]
    [InlineData("54481")]
    [InlineData("54481-1234")]
    public async Task Post_accepts_a_five_digit_or_zip_plus_four_code(string zipCode)
    {
        var response = await _client.PostAsJsonAsync("/api/venues", ValidCreate(zipCode: zipCode));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("544811")]
    [InlineData("54481-")]
    [InlineData("54481-123")]
    [InlineData("abcde")]
    public async Task Post_rejects_a_bad_zip_code_with_a_message_on_ZipCode(string zipCode)
    {
        var response = await _client.PostAsJsonAsync("/api/venues", ValidCreate(zipCode: zipCode));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ZipCode", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("54481-12")]
    [InlineData("5448a")]
    public async Task Put_rejects_a_bad_zip_code_with_a_message_on_ZipCode(string zipCode)
    {
        var response = await _client.PutAsJsonAsync($"/api/venues/{_mainVenueId}", ValidUpdate(zipCode: zipCode));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ZipCode", await response.Content.ReadAsStringAsync());
    }
}

/// <summary>
/// Unit tests for the ZIP code rule itself (CAR-63): the same pattern is used by the API
/// requests and the Razor venue forms, so it's checked here without any web server.
/// </summary>
public class ZipCodeRulesTests
{
    // Runs the attribute on a single value and says whether it passed.
    private static bool IsValid(string? zipCode)
    {
        var attribute = new RegularExpressionAttribute(ZipCodeRules.Pattern);
        return attribute.IsValid(zipCode);
    }

    [Theory]
    [InlineData("54481")]
    [InlineData("00000")]
    [InlineData("99999")]
    [InlineData("54481-1234")]
    [InlineData("00501-0000")]
    public void Accepts_five_digit_and_zip_plus_four_codes(string zipCode)
    {
        Assert.True(IsValid(zipCode));
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("123456")]
    [InlineData("12345-")]
    [InlineData("12345-123")]
    [InlineData("12345-12345")]
    [InlineData("12345 1234")]
    [InlineData("12345_1234")]
    [InlineData(" 12345")]
    [InlineData("12345 ")]
    [InlineData("1234a")]
    [InlineData("abcde")]
    [InlineData("A1B 2C3")]
    [InlineData("12345-abcd")]
    public void Rejects_everything_else(string zipCode)
    {
        Assert.False(IsValid(zipCode));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Skips_empty_values_because_Required_handles_those(string? zipCode)
    {
        // [RegularExpression] ignores null and empty text on purpose. The requests and forms
        // also have [Required], which is what turns a blank ZIP box into an error.
        Assert.True(IsValid(zipCode));
    }
}
