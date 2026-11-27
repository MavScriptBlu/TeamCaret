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
/// GET /api/orders/mine (CAR-95), through real web requests: only your own orders come back,
/// newest first, ticket lines name their event, an empty list when you haven't ordered, and 401
/// when not signed in.
/// </summary>
public sealed class MyOrdersApiTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    // A fresh random password each run, so no credential is ever written in the code.
    private static readonly string TestSecret = $"T{Guid.NewGuid():N}!a1";

    private readonly WebApplicationFactory<Program> _baseFactory;
    private WebApplicationFactory<Program> _factory = null!;
    private SqliteConnection _connection = null!;

    private int _shirtId;
    private int _workshopTicketId;
    private int _workshopEventId;

    public MyOrdersApiTests(WebApplicationFactory<Program> factory)
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

        Product shirt = new() { Category = "Apparel", Name = "Club Shirt", Price = 25m, MemberPrice = 20m, StockQuantity = 10 };
        db.Products.Add(shirt);

        Venue venue = new() { Name = "Room 101", Capacity = 100 };
        DateTime nextWeek = DateTime.UtcNow.AddDays(7);
        Event workshop = new()
        {
            Venue = venue,
            Name = "Linux Workshop",
            StartsAtUtc = nextWeek,
            EndsAtUtc = nextWeek.AddHours(2),
            TicketCapacity = 20,
        };
        db.Events.Add(workshop);

        TicketType workshopTicket = new()
        {
            Event = workshop,
            Category = "Tickets",
            Name = "General",
            Price = 5m,
            MemberPrice = 0m,
            StockQuantity = 0,
        };
        db.TicketTypes.Add(workshopTicket);

        await db.SaveChangesAsync();

        _shirtId = shirt.ProductId;
        _workshopTicketId = workshopTicket.ProductId;
        _workshopEventId = workshop.EventId;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Only_your_own_orders_come_back_newest_first()
    {
        HttpClient me = await SignedInClientAsync();
        HttpClient someoneElse = await SignedInClientAsync();

        OrderDto first = await PlaceAsync(me, (_shirtId, 1));
        await PlaceAsync(someoneElse, (_shirtId, 2));
        OrderDto second = await PlaceAsync(me, (_workshopTicketId, 2));

        List<OrderDto>? mine = await me.GetFromJsonAsync<List<OrderDto>>("/api/orders/mine");

        Assert.NotNull(mine);
        Assert.Equal(new[] { second.OrderId, first.OrderId }, mine.Select(order => order.OrderId));
    }

    [Fact]
    public async Task Ticket_lines_say_which_event_they_are_for()
    {
        HttpClient me = await SignedInClientAsync();
        await PlaceAsync(me, (_shirtId, 1), (_workshopTicketId, 2));

        List<OrderDto>? mine = await me.GetFromJsonAsync<List<OrderDto>>("/api/orders/mine");

        OrderDto order = Assert.Single(mine!);
        Assert.Equal("Pending", order.Status);
        Assert.Null(order.Lines.Single(line => line.ProductId == _shirtId).EventId);
        OrderLineDto tickets = order.Lines.Single(line => line.ProductId == _workshopTicketId);
        Assert.Equal(_workshopEventId, tickets.EventId);
        Assert.Equal(2, tickets.Quantity);
    }

    [Fact]
    public async Task Someone_who_has_not_ordered_gets_an_empty_list()
    {
        HttpClient me = await SignedInClientAsync();

        List<OrderDto>? mine = await me.GetFromJsonAsync<List<OrderDto>>("/api/orders/mine");

        Assert.NotNull(mine);
        Assert.Empty(mine);
    }

    [Fact]
    public async Task Signed_out_gets_401()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/orders/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Registers a new account, logs in and returns a client that sends the bearer token.
    private async Task<HttpClient> SignedInClientAsync()
    {
        HttpClient client = _factory.CreateClient();
        string email = $"buyer-{Guid.NewGuid():N}@ccos.test";

        HttpResponseMessage register = await client.PostAsJsonAsync(
            "/api/account/register",
            new RegisterAccountRequest("Test", "Buyer", email, TestSecret));
        register.EnsureSuccessStatusCode();

        HttpResponseMessage login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestSecret });
        login.EnsureSuccessStatusCode();
        JsonElement body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    // Places an order and returns it.
    private static async Task<OrderDto> PlaceAsync(HttpClient client, params (int ProductId, int Quantity)[] lines)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest(lines.Select(line => new OrderLineRequest(line.ProductId, line.Quantity)).ToList()));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }
}
