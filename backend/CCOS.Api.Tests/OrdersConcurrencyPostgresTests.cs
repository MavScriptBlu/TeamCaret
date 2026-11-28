using System.Net;
using System.Net.Http.Json;
using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CCOS.Api.Tests;

/// <summary>
/// Exercises order concurrency with PostgreSQL because SQLite does not generate xmin values.
/// </summary>
public sealed class OrdersConcurrencyPostgresTests(
    WebApplicationFactory<Program> factory,
    PostgresApiTestFixture postgres) : IClassFixture<WebApplicationFactory<Program>>,
    IClassFixture<PostgresApiTestFixture>, IAsyncLifetime
{
    private WebApplicationFactory<Program> _testFactory = null!;
    private HttpClient _firstTab = null!;
    private HttpClient _secondTab = null!;
    private int _orderId;
    private int _productId;
    private int _eventId;

    public async Task InitializeAsync()
    {
        _testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Testing", "true");
            builder.ConfigureTestServices(services =>
                services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgres.ConnectionString)));
        });

        using (var scope = _testFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            await SeedOrderAsync(db);
        }

        _firstTab = _testFactory.CreateClient();
        _secondTab = _testFactory.CreateClient();
        await TestAccounts.SignInAsOfficerAsync(_testFactory, _firstTab);
        await TestAccounts.SignInAsOfficerAsync(_testFactory, _secondTab);
    }

    public Task DisposeAsync()
    {
        _firstTab.Dispose();
        _secondTab.Dispose();
        _testFactory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Cancel_with_stale_order_copy_returns_conflict_and_restores_inventory_once()
    {
        var firstCopy = await GetOrderAsync(_firstTab);
        var secondCopy = await GetOrderAsync(_secondTab);
        Assert.Equal(firstCopy.RowVersion, secondCopy.RowVersion);

        using var firstResponse = await _firstTab.PutAsJsonAsync(
            $"/api/orders/{_orderId}/status",
            new ChangeOrderStatusRequest(OrderStatuses.Canceled, firstCopy.RowVersion));

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var canceled = await firstResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(canceled);
        Assert.Equal(OrderStatuses.Canceled, canceled.Status);
        Assert.NotEqual(firstCopy.RowVersion, canceled.RowVersion);

        using var staleResponse = await _secondTab.PutAsJsonAsync(
            $"/api/orders/{_orderId}/status",
            new ChangeOrderStatusRequest(OrderStatuses.Canceled, secondCopy.RowVersion));

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        Assert.Equal(8, await LoadMerchandiseStockAsync());
        Assert.Equal(0, await LoadTicketsSoldAsync());
        Assert.Equal(OrderStatuses.Canceled, await LoadOrderStatusAsync());
        Assert.All(await LoadRegistrationsAsync(), registration =>
            Assert.Equal(RegistrationStatus.Canceled, registration.Status));
    }

    private async Task SeedOrderAsync(AppDbContext db)
    {
        var venue = new Venue { Name = $"Order concurrency {Guid.NewGuid():N}", Capacity = 20 };
        var startsAt = DateTime.UtcNow.AddDays(1);
        var @event = new Event
        {
            Venue = venue,
            Name = "Order concurrency event",
            StartsAtUtc = startsAt,
            EndsAtUtc = startsAt.AddHours(1),
            TicketCapacity = 10,
            TicketsSold = 2,
        };
        var merchandise = new Product
        {
            Category = "Apparel",
            Name = "Concurrency test shirt",
            Price = 10m,
            MemberPrice = 8m,
            StockQuantity = 5,
        };
        var ticketType = new TicketType
        {
            Category = "Tickets",
            Name = "Concurrency test admission",
            Price = 10m,
            MemberPrice = 8m,
            Event = @event,
        };
        var order = new Order
        {
            OrderDate = DateTime.UtcNow,
            TotalAmount = 50m,
            Status = OrderStatuses.Pending,
        };

        db.Orders.Add(order);
        db.Products.AddRange(merchandise, ticketType);
        await db.SaveChangesAsync();

        var merchandiseLine = new OrderLine
        {
            Order = order,
            ProductId = merchandise.ProductId,
            Quantity = 3,
            UnitPrice = 10m,
            ProductNameSnapshot = merchandise.Name,
        };
        var ticketLine = new OrderLine
        {
            Order = order,
            ProductId = ticketType.ProductId,
            Quantity = 2,
            UnitPrice = 10m,
            ProductNameSnapshot = ticketType.Name,
        };
        db.OrderLines.AddRange(merchandiseLine, ticketLine);
        await db.SaveChangesAsync();

        db.TicketRegistrations.AddRange(
            new TicketRegistration
            {
                Event = @event,
                TicketTypeId = ticketType.ProductId,
                OrderLine = ticketLine,
                CreatedAtUtc = DateTime.UtcNow,
            },
            new TicketRegistration
            {
                Event = @event,
                TicketTypeId = ticketType.ProductId,
                OrderLine = ticketLine,
                CreatedAtUtc = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        _orderId = order.OrderId;
        _productId = merchandise.ProductId;
        _eventId = @event.EventId;
    }

    private async Task<OrderDto> GetOrderAsync(HttpClient client)
    {
        var orders = await client.GetFromJsonAsync<List<OrderDto>>("/api/orders");
        return orders?.Single(order => order.OrderId == _orderId)
            ?? throw new InvalidOperationException($"Order {_orderId} was not returned by the API.");
    }

    private async Task<string> LoadOrderStatusAsync()
    {
        using var scope = _testFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.AsNoTracking()
            .Where(order => order.OrderId == _orderId)
            .Select(order => order.Status)
            .SingleAsync();
    }

    private async Task<int> LoadMerchandiseStockAsync()
    {
        using var scope = _testFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(product => product.ProductId == _productId)
            .Select(product => product.StockQuantity)
            .SingleAsync();
    }

    private async Task<int> LoadTicketsSoldAsync()
    {
        using var scope = _testFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Events.AsNoTracking()
            .Where(@event => @event.EventId == _eventId)
            .Select(@event => @event.TicketsSold)
            .SingleAsync();
    }

    private async Task<List<TicketRegistration>> LoadRegistrationsAsync()
    {
        using var scope = _testFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TicketRegistrations.AsNoTracking()
            .Where(registration => registration.OrderLine.OrderId == _orderId)
            .ToListAsync();
    }
}
