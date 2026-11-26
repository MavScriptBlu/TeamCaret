using System.Globalization;
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
/// POST /api/orders (CAR-91), through real web requests: a normal order of merch and tickets,
/// member vs. non-member prices, out of stock, sold out, members-only tickets, and the request checks.
/// Every failure case also checks that nothing was saved (no order, stock and seats untouched).
/// </summary>
public sealed class OrdersControllerTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    // A fresh random password each run, so no credential is ever written in the code.
    private static readonly string TestSecret = $"T{Guid.NewGuid():N}!a1";

    private readonly WebApplicationFactory<Program> _baseFactory;
    private WebApplicationFactory<Program> _factory = null!;
    private SqliteConnection _connection = null!;

    // The sample data every test starts with.
    private int _shirtId;
    private int _lastStickerId;
    private int _workshopTicketId;
    private int _workshopEventId;
    private int _nearlyFullTicketId;
    private int _nearlyFullEventId;
    private int _membersOnlyTicketId;
    private int _pastTicketId;

    // The email of the last account SignedInClientAsync made, so a test can look its customer up.
    private string _lastEmail = string.Empty;

    public OrdersControllerTests(WebApplicationFactory<Program> factory)
    {
        _baseFactory = factory;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Setup: a fresh SQLite database with merch, events and tickets
    // ─────────────────────────────────────────────────────────────────────────

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
        Product lastSticker = new() { Category = "Swag", Name = "Holo Sticker", Price = 3m, MemberPrice = 2m, StockQuantity = 1 };
        db.Products.AddRange(shirt, lastSticker);

        Venue venue = new() { Name = "Room 101", Capacity = 100 };
        DateTime nextWeek = DateTime.UtcNow.AddDays(7);
        Event workshop = NewEvent(venue, "Linux Workshop", nextWeek, capacity: 20);
        Event nearlyFull = NewEvent(venue, "CTF Night", nextWeek, capacity: 5, sold: 4);
        Event membersOnly = NewEvent(venue, "Hackathon", nextWeek, capacity: 30, membersOnly: true);
        Event past = NewEvent(venue, "Fall Kickoff", DateTime.UtcNow.AddDays(-7), capacity: 50);
        db.Events.AddRange(workshop, nearlyFull, membersOnly, past);

        TicketType workshopTicket = NewTicket(workshop, price: 5m, memberPrice: 0m);
        TicketType nearlyFullTicket = NewTicket(nearlyFull, price: 10m, memberPrice: 5m);
        TicketType membersOnlyTicket = NewTicket(membersOnly, price: 0m, memberPrice: 0m);
        TicketType pastTicket = NewTicket(past, price: 3m, memberPrice: 1m);
        db.TicketTypes.AddRange(workshopTicket, nearlyFullTicket, membersOnlyTicket, pastTicket);

        await db.SaveChangesAsync();

        _shirtId = shirt.ProductId;
        _lastStickerId = lastSticker.ProductId;
        _workshopTicketId = workshopTicket.ProductId;
        _workshopEventId = workshop.EventId;
        _nearlyFullTicketId = nearlyFullTicket.ProductId;
        _nearlyFullEventId = nearlyFull.EventId;
        _membersOnlyTicketId = membersOnlyTicket.ProductId;
        _pastTicketId = pastTicket.ProductId;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Normal orders
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_normal_order_saves_merch_and_tickets_as_one_pending_order()
    {
        HttpClient client = await SignedInClientAsync(isMember: false);

        HttpResponseMessage response = await PlaceAsync(client, (_shirtId, 2), (_workshopTicketId, 3));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        OrderDto? order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.True(order.OrderId > 0);
        Assert.Equal("Pending", order.Status);
        Assert.Equal(65m, order.TotalAmount); // 2 x 25 + 3 x 5

        OrderLineDto shirtLine = Assert.Single(order.Lines, line => line.ProductId == _shirtId);
        Assert.Equal("Club Shirt", shirtLine.Name);
        Assert.Null(shirtLine.EventId);
        Assert.Equal(50m, shirtLine.LineTotal);

        OrderLineDto ticketLine = Assert.Single(order.Lines, line => line.ProductId == _workshopTicketId);
        Assert.Equal(_workshopEventId, ticketLine.EventId);

        // Stock went down, seats are held, and there's one registration per ticket.
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(8, (await db.Products.AsNoTracking().SingleAsync(product => product.ProductId == _shirtId)).StockQuantity);
        Assert.Equal(3, (await db.Events.AsNoTracking().SingleAsync(@event => @event.EventId == _workshopEventId)).TicketsSold);

        List<TicketRegistration> registrations = await db.TicketRegistrations.AsNoTracking()
            .Where(registration => registration.OrderLine.OrderId == order.OrderId)
            .ToListAsync();
        Assert.Equal(3, registrations.Count);
        Assert.All(registrations, registration =>
        {
            Assert.Equal(_workshopEventId, registration.EventId);
            Assert.Equal(_workshopTicketId, registration.TicketTypeId);
            Assert.Equal(RegistrationStatus.Purchased, registration.Status);
        });
    }

    [Fact]
    public async Task The_order_belongs_to_the_signed_in_customer()
    {
        HttpClient client = await SignedInClientAsync(isMember: false);

        OrderDto? order = await (await PlaceAsync(client, (_shirtId, 1))).Content.ReadFromJsonAsync<OrderDto>();

        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Order saved = await db.Orders.AsNoTracking().SingleAsync(candidate => candidate.OrderId == order!.OrderId);
        int expectedCustomerId = await db.Users.AsNoTracking()
            .Where(user => user.Email == _lastEmail)
            .Select(user => user.CustomerId)
            .SingleAsync();
        Assert.Equal(expectedCustomerId, saved.CustomerId);
    }

    [Fact]
    public async Task The_same_product_on_two_lines_is_added_together()
    {
        HttpClient client = await SignedInClientAsync(isMember: false);

        OrderDto? order = await (await PlaceAsync(client, (_shirtId, 1), (_shirtId, 2))).Content.ReadFromJsonAsync<OrderDto>();

        OrderLineDto line = Assert.Single(order!.Lines);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(75m, order.TotalAmount);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Prices: member price only for current members
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true, 20, 0, 20)]   // member: shirt 20, workshop ticket 0
    [InlineData(false, 25, 5, 30)]  // not a member: shirt 25, workshop ticket 5
    public async Task Members_pay_member_prices_and_everyone_else_pays_regular(
        bool isMember,
        int expectedShirtPrice,
        int expectedTicketPrice,
        int expectedTotal)
    {
        HttpClient client = await SignedInClientAsync(isMember);

        OrderDto? order = await (await PlaceAsync(client, (_shirtId, 1), (_workshopTicketId, 1))).Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(expectedShirtPrice, Assert.Single(order!.Lines, line => line.ProductId == _shirtId).UnitPrice);
        Assert.Equal(expectedTicketPrice, Assert.Single(order.Lines, line => line.ProductId == _workshopTicketId).UnitPrice);
        Assert.Equal(expectedTotal, order.TotalAmount);
    }

    [Fact]
    public async Task An_expired_member_pays_the_regular_price()
    {
        HttpClient client = await SignedInClientAsync(isMember: true, membershipExpired: true);

        OrderDto? order = await (await PlaceAsync(client, (_shirtId, 1))).Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(25m, Assert.Single(order!.Lines).UnitPrice);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Out of stock, sold out, not on sale: 409 and nothing saved
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Out_of_stock_merch_is_a_409_that_names_it_and_saves_nothing()
    {
        HttpClient client = await SignedInClientAsync(isMember: false);

        // The shirt is fine, the sticker only has 1 left, and the ticket is fine.
        HttpResponseMessage response = await PlaceAsync(client, (_shirtId, 1), (_lastStickerId, 2), (_workshopTicketId, 1));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, _lastStickerId, "Holo Sticker");
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task A_sold_out_event_is_a_409_that_names_it_and_saves_nothing()
    {
        HttpClient client = await SignedInClientAsync(isMember: false);

        // CTF Night has 1 seat left. The shirt's stock was taken first, so this also proves it rolls back.
        HttpResponseMessage response = await PlaceAsync(client, (_shirtId, 2), (_nearlyFullTicketId, 2));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, _nearlyFullTicketId, "CTF Night");
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task The_last_seat_can_still_be_bought()
    {
        HttpClient client = await SignedInClientAsync(isMember: false);

        HttpResponseMessage response = await PlaceAsync(client, (_nearlyFullTicketId, 1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(5, (await db.Events.AsNoTracking().SingleAsync(@event => @event.EventId == _nearlyFullEventId)).TicketsSold);
    }

    [Fact]
    public async Task Tickets_for_an_event_that_already_started_are_a_409()
    {
        HttpClient client = await SignedInClientAsync(isMember: true);

        HttpResponseMessage response = await PlaceAsync(client, (_pastTicketId, 1));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, _pastTicketId, "Fall Kickoff");
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task A_product_that_was_removed_is_a_409()
    {
        HttpClient client = await SignedInClientAsync(isMember: false);
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Product shirt = await db.Products.SingleAsync(product => product.ProductId == _shirtId);
            shirt.IsActive = false;
            await db.SaveChangesAsync();
        }

        HttpResponseMessage response = await PlaceAsync(client, (_shirtId, 1));

        // The message names the shirt, never its raw id.
        string detail = await AssertProblemAsync(response, HttpStatusCode.Conflict, _shirtId, "Club Shirt isn't for sale anymore");
        Assert.DoesNotContain(_shirtId.ToString(CultureInfo.InvariantCulture), detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_ticket_type_that_was_removed_names_its_event()
    {
        HttpClient client = await SignedInClientAsync(isMember: false);
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Product workshopTicket = await db.Products.SingleAsync(product => product.ProductId == _workshopTicketId);
            workshopTicket.IsActive = false;
            await db.SaveChangesAsync();
        }

        HttpResponseMessage response = await PlaceAsync(client, (_workshopTicketId, 1));

        // "Tickets for Linux Workshop" tells them way more than "General" or an id would.
        string detail = await AssertProblemAsync(response, HttpStatusCode.Conflict, _workshopTicketId, "Tickets for Linux Workshop");
        Assert.DoesNotContain(_workshopTicketId.ToString(CultureInfo.InvariantCulture), detail, StringComparison.Ordinal);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task An_id_that_never_existed_gets_a_plain_message_with_no_id()
    {
        HttpClient client = await SignedInClientAsync(isMember: false);
        const int madeUpProductId = 987654;

        HttpResponseMessage response = await PlaceAsync(client, (madeUpProductId, 1));

        // No name to show, so keep it general and still keep the id out of the sentence.
        string detail = await AssertProblemAsync(response, HttpStatusCode.Conflict, madeUpProductId, "One of the items in your cart");
        Assert.DoesNotContain(madeUpProductId.ToString(CultureInfo.InvariantCulture), detail, StringComparison.Ordinal);
        await AssertNothingSavedAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Members-only events
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Members_only_tickets_are_refused_for_non_members()
    {
        HttpClient client = await SignedInClientAsync(isMember: false);

        HttpResponseMessage response = await PlaceAsync(client, (_shirtId, 1), (_membersOnlyTicketId, 1));

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, _membersOnlyTicketId, "Hackathon");
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Members_can_get_members_only_tickets()
    {
        HttpClient client = await SignedInClientAsync(isMember: true);

        HttpResponseMessage response = await PlaceAsync(client, (_membersOnlyTicketId, 1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Request checks
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ordering_needs_a_sign_in()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await PlaceAsync(client, (_shirtId, 1));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_empty_order_is_a_400()
    {
        HttpClient client = await SignedInClientAsync(isMember: false);

        HttpResponseMessage response = await PlaceAsync(client);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task A_bad_quantity_is_a_400(int quantity)
    {
        HttpClient client = await SignedInClientAsync(isMember: false);

        HttpResponseMessage response = await PlaceAsync(client, (_shirtId, quantity));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNothingSavedAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a new account, optionally gives it a membership, logs in and returns a client
    /// that sends the bearer token.
    /// </summary>
    private async Task<HttpClient> SignedInClientAsync(bool isMember, bool membershipExpired = false)
    {
        HttpClient client = _factory.CreateClient();
        string email = $"buyer-{Guid.NewGuid():N}@ccos.test";
        _lastEmail = email;

        HttpResponseMessage register = await client.PostAsJsonAsync(
            "/api/account/register",
            new RegisterAccountRequest("Test", "Buyer", email, TestSecret));
        register.EnsureSuccessStatusCode();

        if (isMember)
        {
            using IServiceScope scope = _factory.Services.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            AppUser user = await db.Users.AsNoTracking().SingleAsync(account => account.Email == email);
            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            DateOnly expiration = membershipExpired ? today.AddMonths(-1) : today.AddMonths(6);
            db.Members.Add(new Member { MemberId = user.CustomerId, JoinedDate = today.AddYears(-1), MemberExpiration = expiration });
            await db.SaveChangesAsync();
        }

        HttpResponseMessage login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestSecret });
        login.EnsureSuccessStatusCode();
        JsonElement body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    /// <summary>POSTs an order made of (product id, quantity) pairs.</summary>
    private static Task<HttpResponseMessage> PlaceAsync(HttpClient client, params (int ProductId, int Quantity)[] lines) =>
        client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest(lines.Select(line => new OrderLineRequest(line.ProductId, line.Quantity)).ToList()));

    /// <summary>
    /// Checks the status code, that productId points at the item that stopped the order, and
    /// (when given) that the message names it. Hands back the message so a test can check more.
    /// </summary>
    private static async Task<string> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        int expectedProductId,
        string? expectedNameInMessage)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedProductId, problem.GetProperty("productId").GetInt32());

        string detail = problem.GetProperty("detail").GetString() ?? string.Empty;
        Assert.False(string.IsNullOrWhiteSpace(detail));
        if (expectedNameInMessage is not null)
        {
            Assert.Contains(expectedNameInMessage, detail, StringComparison.Ordinal);
        }

        return detail;
    }

    /// <summary>No orders, no registrations, and every stock and seat count is back where setup left it.</summary>
    private async Task AssertNothingSavedAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(0, await db.Orders.CountAsync());
        Assert.Equal(0, await db.OrderLines.CountAsync());
        Assert.Equal(0, await db.TicketRegistrations.CountAsync());
        Assert.Equal(10, (await db.Products.AsNoTracking().SingleAsync(product => product.ProductId == _shirtId)).StockQuantity);
        Assert.Equal(1, (await db.Products.AsNoTracking().SingleAsync(product => product.ProductId == _lastStickerId)).StockQuantity);
        Assert.Equal(0, (await db.Events.AsNoTracking().SingleAsync(@event => @event.EventId == _workshopEventId)).TicketsSold);
        Assert.Equal(4, (await db.Events.AsNoTracking().SingleAsync(@event => @event.EventId == _nearlyFullEventId)).TicketsSold);
    }

    /// <summary>A two-hour event at the venue.</summary>
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

    /// <summary>The event's "General" ticket type.</summary>
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
