using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CCOS.Api.Tests;

public class EventsControllerTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private SqliteConnection _connection = null!;
    private IServiceScope _scope = null!;
    private AppDbContext _db = null!;
    private Venue _activeVenue = null!;
    private Venue _inactiveVenue = null!;

    public EventsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var testFactory = CreateSqliteFactory();
        _scope = testFactory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await _db.Database.EnsureCreatedAsync();

        _activeVenue = new Venue { Name = "Active Test Venue", Capacity = 50, IsActive = true };
        _inactiveVenue = new Venue { Name = "Inactive Test Venue", Capacity = 50, IsActive = false };
        _db.Venues.AddRange(_activeVenue, _inactiveVenue);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _scope.Dispose();
        await _connection.CloseAsync();
        _connection.Dispose();
    }

    [Fact]
    public async Task Create_SavesEventAndFirstTicketType_ReturnsCreatedWithLocation()
    {
        var client = CreateClient();
        var request = CreateRequest();

        var response = await client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/api/events/", response.Headers.Location.ToString());

        var created = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.NotNull(created);
        Assert.Single(created.TicketTypes);
        Assert.Equal("General", created.TicketTypes[0].Name);
        Assert.Equal(request.Price, created.TicketTypes[0].Price);
        Assert.Equal(request.MemberPrice, created.TicketTypes[0].MemberPrice);

        _db.ChangeTracker.Clear();
        var savedEvent = await _db.Events
            .Include(@event => @event.TicketTypes)
            .SingleAsync(@event => @event.EventId == created.EventId);
        Assert.Single(savedEvent.TicketTypes);
        Assert.Equal(request.Name, savedEvent.Name);
    }

    [Fact]
    public async Task Update_ExistingEvent_UpdatesAllowedFields()
    {
        var originalStart = DateTimeOffset.UtcNow.AddDays(20);
        var @event = await AddEventAsync(originalStart, originalStart.AddHours(2));
        var client = CreateClient();
        var current = await client.GetFromJsonAsync<EventDto>($"/api/events/{@event.EventId}");
        Assert.NotNull(current);

        var newStart = originalStart.AddDays(1);
        var request = new UpdateEventRequest(
            _activeVenue.VenueId,
            "Updated event",
            "Updated description",
            newStart,
            newStart.AddHours(3),
            25,
            true,
            current.RowVersion);

        var response = await client.PutAsJsonAsync($"/api/events/{@event.EventId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated event", updated.Name);
        Assert.Equal("Updated description", updated.Description);
        Assert.Equal(25, updated.TicketCapacity);
        Assert.True(updated.MembersOnly);
        Assert.Equal(newStart.UtcDateTime, updated.StartsAtUtc);
    }

    [Fact]
    public async Task Update_UnknownEvent_ReturnsNotFound()
    {
        var client = CreateClient();
        var request = UpdateRequest(DateTimeOffset.UtcNow.AddDays(2), rowVersion: 0);

        var response = await client.PutAsJsonAsync("/api/events/99999", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_DoesNotChangeTicketPricesOrTicketsSold()
    {
        var start = DateTimeOffset.UtcNow.AddDays(20);
        var @event = await AddEventAsync(start, start.AddHours(2), ticketsSold: 3);
        var ticketType = await AddTicketTypeAsync(@event, 12m, 8m);
        var client = CreateClient();
        var current = await client.GetFromJsonAsync<EventDto>($"/api/events/{@event.EventId}");
        Assert.NotNull(current);

        var request = UpdateRequest(start.AddDays(1), rowVersion: current.RowVersion);
        var response = await client.PutAsJsonAsync($"/api/events/{@event.EventId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _db.ChangeTracker.Clear();
        var savedEvent = await _db.Events.SingleAsync(item => item.EventId == @event.EventId);
        var savedTicket = await _db.TicketTypes.SingleAsync(item => item.ProductId == ticketType.ProductId);
        Assert.Equal(3, savedEvent.TicketsSold);
        Assert.Equal(12m, savedTicket.Price);
        Assert.Equal(8m, savedTicket.MemberPrice);
    }

    [Fact]
    public async Task Deactivate_KeepsEventAndRegistrationsAndHidesEventFromPublicReads()
    {
        var start = DateTimeOffset.UtcNow.AddDays(20);
        var @event = await AddEventAsync(start, start.AddHours(2), ticketsSold: 1);
        var ticketType = await AddTicketTypeAsync(@event, 10m, 5m);
        await AddRegistrationAsync(@event, ticketType);
        var client = CreateClient();

        var response = await client.DeleteAsync($"/api/events/{@event.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        _db.ChangeTracker.Clear();
        var savedEvent = await _db.Events.SingleAsync(item => item.EventId == @event.EventId);
        var registrationCount = await _db.TicketRegistrations.CountAsync(
            registration => registration.EventId == @event.EventId);
        Assert.False(savedEvent.IsActive);
        Assert.Equal(1, registrationCount);

        var list = await client.GetFromJsonAsync<List<EventDto>>("/api/events");
        Assert.NotNull(list);
        Assert.DoesNotContain(list, item => item.EventId == @event.EventId);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/events/{@event.EventId}")).StatusCode);
    }

    [Fact]
    public async Task Deactivate_AlreadyInactiveEvent_ReturnsNotFound()
    {
        var start = DateTimeOffset.UtcNow.AddDays(20);
        var @event = await AddEventAsync(start, start.AddHours(2), isActive: false);
        var client = CreateClient();

        var response = await client.DeleteAsync($"/api/events/{@event.EventId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_BlankName_ReturnsFieldValidationError()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/events", CreateRequest(name: "   "));

        await AssertFieldErrorAsync(response, nameof(CreateEventRequest.Name), "Name is required.");
    }

    [Fact]
    public async Task Create_EndTimeNotAfterStart_ReturnsFieldValidationError()
    {
        var start = DateTimeOffset.UtcNow.AddDays(20);
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/events",
            CreateRequest(startsAtUtc: start, endsAtUtc: start));

        await AssertFieldErrorAsync(response, nameof(CreateEventRequest.EndsAtUtc), "End time must be after start time.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_NonPositiveCapacity_ReturnsFieldValidationError(int capacity)
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/events",
            CreateRequest(ticketCapacity: capacity));

        await AssertFieldErrorAsync(response, nameof(CreateEventRequest.TicketCapacity), "Ticket capacity must be greater than 0.");
    }

    [Fact]
    public async Task Update_CapacityBelowTicketsSold_ReturnsFieldValidationError()
    {
        var start = DateTimeOffset.UtcNow.AddDays(20);
        var @event = await AddEventAsync(start, start.AddHours(2), ticketsSold: 5);
        var client = CreateClient();
        var current = await client.GetFromJsonAsync<EventDto>($"/api/events/{@event.EventId}");
        Assert.NotNull(current);

        var request = UpdateRequest(start.AddDays(1), ticketCapacity: 4, rowVersion: current.RowVersion);
        var response = await client.PutAsJsonAsync($"/api/events/{@event.EventId}", request);

        await AssertFieldErrorAsync(response, nameof(UpdateEventRequest.TicketCapacity), "tickets already sold");
    }

    [Fact]
    public async Task Create_CapacityAboveVenueCapacity_ReturnsFieldValidationError()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/events",
            CreateRequest(ticketCapacity: _activeVenue.Capacity + 1));

        await AssertFieldErrorAsync(response, nameof(CreateEventRequest.TicketCapacity), "venue's capacity");
    }

    [Fact]
    public async Task Create_MissingVenue_ReturnsFieldValidationError()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/events",
            CreateRequest(venueId: 99999));

        await AssertFieldErrorAsync(response, nameof(CreateEventRequest.VenueId), "Venue does not exist or is inactive.");
    }

    [Fact]
    public async Task Create_InactiveVenue_ReturnsFieldValidationError()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/events",
            CreateRequest(venueId: _inactiveVenue.VenueId));

        await AssertFieldErrorAsync(response, nameof(CreateEventRequest.VenueId), "Venue does not exist or is inactive.");
    }

    [Fact]
    public async Task Create_OverlappingActiveEventAtSameVenue_ReturnsFieldValidationError()
    {
        var start = DateTimeOffset.UtcNow.AddDays(20);
        await AddEventAsync(start, start.AddHours(2));
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/events",
            CreateRequest(startsAtUtc: start.AddMinutes(30), endsAtUtc: start.AddHours(3)));

        await AssertFieldErrorAsync(response, nameof(CreateEventRequest.StartsAtUtc), "already has an active event");
    }

    [Fact]
    public async Task Create_BackToBackEvent_IsAllowed()
    {
        var start = DateTimeOffset.UtcNow.AddDays(20);
        await AddEventAsync(start, start.AddHours(2));
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/events",
            CreateRequest(startsAtUtc: start.AddHours(2), endsAtUtc: start.AddHours(4)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_OverlapWithInactiveEvent_IsAllowed()
    {
        var start = DateTimeOffset.UtcNow.AddDays(20);
        await AddEventAsync(start, start.AddHours(2), isActive: false);
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/events",
            CreateRequest(startsAtUtc: start.AddMinutes(30), endsAtUtc: start.AddHours(3)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Update_MovingEventOntoItsCurrentTime_DoesNotConflictWithItself()
    {
        var start = DateTimeOffset.UtcNow.AddDays(20);
        var @event = await AddEventAsync(start, start.AddHours(2));
        var client = CreateClient();
        var current = await client.GetFromJsonAsync<EventDto>($"/api/events/{@event.EventId}");
        Assert.NotNull(current);

        var request = new UpdateEventRequest(
            _activeVenue.VenueId,
            current.Name,
            current.Description,
            new DateTimeOffset(current.StartsAtUtc),
            new DateTimeOffset(current.EndsAtUtc),
            current.TicketCapacity,
            current.MembersOnly,
            current.RowVersion);

        var response = await client.PutAsJsonAsync($"/api/events/{@event.EventId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_MovingEventIntoAnotherActiveEvent_ReturnsFieldValidationError()
    {
        var start = DateTimeOffset.UtcNow.AddDays(20);
        var firstEvent = await AddEventAsync(start, start.AddHours(2));
        var movingEvent = await AddEventAsync(start.AddDays(1), start.AddDays(1).AddHours(2));
        var client = CreateClient();
        var current = await client.GetFromJsonAsync<EventDto>($"/api/events/{movingEvent.EventId}");
        Assert.NotNull(current);

        var request = new UpdateEventRequest(
            _activeVenue.VenueId,
            current.Name,
            current.Description,
            start.AddHours(1),
            start.AddHours(3),
            current.TicketCapacity,
            current.MembersOnly,
            current.RowVersion);

        var response = await client.PutAsJsonAsync($"/api/events/{movingEvent.EventId}", request);

        await AssertFieldErrorAsync(response, nameof(UpdateEventRequest.StartsAtUtc), "already has an active event");
        Assert.NotEqual(firstEvent.EventId, movingEvent.EventId);
    }

    [Fact(Skip = "Pending CAR-69: add the Officer policy and an authenticated non-officer test user.")]
    public Task Create_AsNonOfficer_ReturnsForbidden_PendingCar69() => Task.CompletedTask;

    private WebApplicationFactory<Program> CreateSqliteFactory() => _factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Testing", "true");
        builder.ConfigureTestServices(services =>
        {
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    });

    private HttpClient CreateClient() => CreateSqliteFactory().CreateClient();

    private CreateEventRequest CreateRequest(
        int? venueId = null,
        string name = "Test event",
        DateTimeOffset? startsAtUtc = null,
        DateTimeOffset? endsAtUtc = null,
        int ticketCapacity = 20) 
    {
        var start = startsAtUtc ?? DateTimeOffset.UtcNow.AddDays(30);
        return new CreateEventRequest(
            venueId ?? _activeVenue.VenueId,
            name,
            "A test event",
            start,
            endsAtUtc ?? start.AddHours(2),
            ticketCapacity,
            false,
            20m,
            10m);
    }

    private UpdateEventRequest UpdateRequest(
        DateTimeOffset startsAtUtc,
        int ticketCapacity = 20,
        uint rowVersion = 0) => new(
            _activeVenue.VenueId,
            "Updated test event",
            "Updated description",
            startsAtUtc,
            startsAtUtc.AddHours(2),
            ticketCapacity,
            false,
            rowVersion);

    private async Task<Event> AddEventAsync(
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        bool isActive = true,
        int ticketCapacity = 20,
        int ticketsSold = 0)
    {
        var @event = new Event
        {
            VenueId = _activeVenue.VenueId,
            Name = $"Existing event {Guid.NewGuid():N}",
            Description = "Seeded event",
            StartsAtUtc = startsAtUtc.UtcDateTime,
            EndsAtUtc = endsAtUtc.UtcDateTime,
            TicketCapacity = ticketCapacity,
            TicketsSold = ticketsSold,
            MembersOnly = false,
            IsActive = isActive
        };

        _db.Events.Add(@event);
        await _db.SaveChangesAsync();
        return @event;
    }

    private async Task<TicketType> AddTicketTypeAsync(Event @event, decimal price, decimal memberPrice)
    {
        var ticketType = new TicketType
        {
            EventId = @event.EventId,
            Category = "Tickets",
            Name = "General",
            Price = price,
            MemberPrice = memberPrice,
            StockQuantity = 0,
            IsActive = true
        };

        _db.TicketTypes.Add(ticketType);
        await _db.SaveChangesAsync();
        return ticketType;
    }

    private async Task AddRegistrationAsync(Event @event, TicketType ticketType)
    {
        var order = new Order
        {
            OrderDate = DateTime.UtcNow,
            Status = "Pending",
            TotalAmount = ticketType.Price
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var orderLine = new OrderLine
        {
            OrderId = order.OrderId,
            ProductId = ticketType.ProductId,
            Quantity = 1,
            UnitPrice = ticketType.Price,
            ProductNameSnapshot = ticketType.Name
        };
        _db.OrderLines.Add(orderLine);
        await _db.SaveChangesAsync();

        _db.TicketRegistrations.Add(new TicketRegistration
        {
            EventId = @event.EventId,
            TicketTypeId = ticketType.ProductId,
            OrderLineId = orderLine.OrderLineId,
            Status = RegistrationStatus.Purchased,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private static async Task AssertFieldErrorAsync(
        HttpResponseMessage response,
        string field,
        string expectedMessageFragment)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out var messages), $"Expected an error for {field}.");
        Assert.Contains(expectedMessageFragment, messages[0].GetString());
    }
}

public class EventsConcurrencyTests : IClassFixture<WebApplicationFactory<Program>>,
    IClassFixture<PostgresApiTestFixture>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly PostgresApiTestFixture _postgres;
    private WebApplicationFactory<Program> _testFactory = null!;
    private IServiceScope _scope = null!;
    private AppDbContext _db = null!;
    private HttpClient _client = null!;
    private int _eventId;
    private int _venueId;

    public EventsConcurrencyTests(
        WebApplicationFactory<Program> factory,
        PostgresApiTestFixture postgres)
    {
        _factory = factory;
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _testFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Testing", "true");
            builder.ConfigureTestServices(services =>
            {
                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(_postgres.ConnectionString));
            });
        });

        _scope = _testFactory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await _db.Database.EnsureCreatedAsync();

        var venue = new Venue { Name = "Concurrency Test Venue", Capacity = 100, IsActive = true };
        var startsAt = DateTime.UtcNow.AddDays(10);
        var @event = new Event
        {
            Venue = venue,
            Name = "Concurrency test event",
            StartsAtUtc = startsAt,
            EndsAtUtc = startsAt.AddHours(2),
            TicketCapacity = 50,
            TicketsSold = 0,
            MembersOnly = false,
            IsActive = true
        };

        _db.Events.Add(@event);
        await _db.SaveChangesAsync();
        _venueId = venue.VenueId;
        _eventId = @event.EventId;
        _client = _testFactory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _scope.Dispose();
        _testFactory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Update_StaleRowVersion_ReturnsConflict()
    {
        var firstCopy = await _client.GetFromJsonAsync<EventDto>($"/api/events/{_eventId}");
        var secondCopy = await _client.GetFromJsonAsync<EventDto>($"/api/events/{_eventId}");
        Assert.NotNull(firstCopy);
        Assert.NotNull(secondCopy);
        Assert.Equal(firstCopy.RowVersion, secondCopy.RowVersion);

        var firstRequest = new UpdateEventRequest(
            _venueId,
            "First edit",
            firstCopy.Description,
            new DateTimeOffset(firstCopy.StartsAtUtc),
            new DateTimeOffset(firstCopy.EndsAtUtc),
            firstCopy.TicketCapacity,
            firstCopy.MembersOnly,
            firstCopy.RowVersion);
        var firstResponse = await _client.PutAsJsonAsync($"/api/events/{_eventId}", firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var staleRequest = new UpdateEventRequest(
            _venueId,
            "Second stale edit",
            secondCopy.Description,
            new DateTimeOffset(secondCopy.StartsAtUtc),
            new DateTimeOffset(secondCopy.EndsAtUtc),
            secondCopy.TicketCapacity,
            secondCopy.MembersOnly,
            secondCopy.RowVersion);
        var staleResponse = await _client.PutAsJsonAsync($"/api/events/{_eventId}", staleRequest);

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
    }
}
