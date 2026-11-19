using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CCOS.Api.Tests;

/// <summary>
/// POST /api/orders (CAR-91) against real PostgreSQL, the production database: a normal order
/// goes through, and when lots of people race for the last item or the last seat, exactly one wins.
/// SQLite runs one write at a time, so only PostgreSQL can show the race is handled.
/// </summary>
public sealed class OrdersPostgresTests(
    WebApplicationFactory<Program> factory,
    PostgresApiTestFixture postgres) : IClassFixture<WebApplicationFactory<Program>>,
    IClassFixture<PostgresApiTestFixture>, IAsyncLifetime
{
    // A fresh random password each run, so no credential is ever written in the code.
    private static readonly string TestSecret = $"T{Guid.NewGuid():N}!a1";

    // How many buyers hit the endpoint at the same moment in the race tests.
    private const int RacingBuyers = 10;

    private WebApplicationFactory<Program> _testFactory = null!;
    private int _lastHoodieId;
    private int _lastSeatTicketId;
    private int _lastSeatEventId;

    // ─────────────────────────────────────────────────────────────────────────
    // Setup: one hoodie left, one seat left
    // ─────────────────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        _testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Testing", "true");
            builder.ConfigureTestServices(services =>
                services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgres.ConnectionString)));
        });

        using IServiceScope scope = _testFactory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        Product lastHoodie = new() { Category = "Apparel", Name = $"Last Hoodie {Guid.NewGuid():N}", Price = 40m, MemberPrice = 35m, StockQuantity = 1 };
        db.Products.Add(lastHoodie);

        Venue venue = new() { Name = $"Orders Venue {Guid.NewGuid():N}", Capacity = 100 };
        DateTime startsAt = DateTime.UtcNow.AddDays(10);
        Event lastSeatEvent = new()
        {
            Venue = venue,
            Name = "Packed Workshop",
            StartsAtUtc = startsAt,
            EndsAtUtc = startsAt.AddHours(2),
            TicketCapacity = 10,
            TicketsSold = 9,
        };
        TicketType lastSeatTicket = new() { Event = lastSeatEvent, Category = "Tickets", Name = "General", Price = 5m, MemberPrice = 0m, StockQuantity = 0 };
        db.Events.Add(lastSeatEvent);
        db.TicketTypes.Add(lastSeatTicket);
        await db.SaveChangesAsync();

        _lastHoodieId = lastHoodie.ProductId;
        _lastSeatTicketId = lastSeatTicket.ProductId;
        _lastSeatEventId = lastSeatEvent.EventId;
    }

    public Task DisposeAsync()
    {
        _testFactory.Dispose();
        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Racing_for_the_last_item_only_one_order_wins()
    {
        HttpClient client = await SignedInClientAsync();

        List<HttpStatusCode> results = await RaceAsync(client, _lastHoodieId);

        Assert.Equal(1, results.Count(status => status == HttpStatusCode.Created));
        Assert.Equal(RacingBuyers - 1, results.Count(status => status == HttpStatusCode.Conflict));

        using IServiceScope scope = _testFactory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, (await db.Products.AsNoTracking().SingleAsync(product => product.ProductId == _lastHoodieId)).StockQuantity);
        Assert.Equal(1, await db.OrderLines.CountAsync(line => line.ProductId == _lastHoodieId));
    }

    [Fact]
    public async Task Racing_for_the_last_seat_only_one_order_wins()
    {
        HttpClient client = await SignedInClientAsync();

        List<HttpStatusCode> results = await RaceAsync(client, _lastSeatTicketId);

        Assert.Equal(1, results.Count(status => status == HttpStatusCode.Created));
        Assert.Equal(RacingBuyers - 1, results.Count(status => status == HttpStatusCode.Conflict));

        using IServiceScope scope = _testFactory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(10, (await db.Events.AsNoTracking().SingleAsync(@event => @event.EventId == _lastSeatEventId)).TicketsSold);
        Assert.Equal(1, await db.TicketRegistrations.CountAsync(registration => registration.EventId == _lastSeatEventId));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Fires the same one-item order RacingBuyers times at once and collects the status codes.</summary>
    private static async Task<List<HttpStatusCode>> RaceAsync(HttpClient client, int productId)
    {
        PlaceOrderRequest request = new([new OrderLineRequest(productId, 1)]);
        IEnumerable<Task<HttpResponseMessage>> requests = Enumerable.Range(0, RacingBuyers)
            .Select(_ => client.PostAsJsonAsync("/api/orders", request));

        HttpResponseMessage[] responses = await Task.WhenAll(requests);
        return responses.Select(response => response.StatusCode).ToList();
    }

    /// <summary>Registers a non-member account and returns a client that sends its bearer token.</summary>
    private async Task<HttpClient> SignedInClientAsync()
    {
        HttpClient client = _testFactory.CreateClient();
        string email = $"racer-{Guid.NewGuid():N}@ccos.test";

        HttpResponseMessage register = await client.PostAsJsonAsync(
            "/api/account/register",
            new RegisterAccountRequest("Test", "Racer", email, TestSecret));
        register.EnsureSuccessStatusCode();

        HttpResponseMessage login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestSecret });
        login.EnsureSuccessStatusCode();
        JsonElement body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }
}
