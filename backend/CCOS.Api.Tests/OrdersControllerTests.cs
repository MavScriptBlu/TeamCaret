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

public sealed class OrdersControllerTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private WebApplicationFactory<Program> _testFactory = null!;
    private SqliteConnection _connection = null!;
    private HttpClient _officerClient = null!;
    private int _orderId;
    private int _paidOrderId;
    private int _merchandiseId;
    private int _eventId;
    private DateTime _orderDateUtc;

    public OrdersControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        _testFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Testing", "true");
            builder.ConfigureTestServices(services =>
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection)));
        });

        using (var scope = _testFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            await SeedOrdersAsync(db);
        }

        _officerClient = _testFactory.CreateClient();
        await TestAccounts.SignInAsOfficerAsync(_testFactory, _officerClient);
    }

    public async Task DisposeAsync()
    {
        _officerClient.Dispose();
        _testFactory.Dispose();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task List_returns_order_snapshot_and_filters_by_status()
    {
        var response = await _officerClient.GetAsync("/api/orders");
        response.EnsureSuccessStatusCode();
        var orders = await response.Content.ReadFromJsonAsync<List<OrderDto>>();

        Assert.NotNull(orders);
        Assert.Equal(2, orders.Count);
        var order = Assert.Single(orders, item => item.OrderId == _orderId);
        Assert.Equal("Alex Customer", order.CustomerName);
        Assert.Equal(DateTime.SpecifyKind(_orderDateUtc, DateTimeKind.Unspecified), order.OrderDateUtc);
        Assert.Equal(50m, order.TotalAmount);
        Assert.Equal(OrderStatuses.Pending, order.Status);
        Assert.Equal(2, order.Items.Count);
        Assert.Contains(order.Items, item => item.ProductName == "Snapshot Shirt"
            && item.Quantity == 3 && item.UnitPrice == 10m && item.LineTotal == 30m);
        Assert.Contains(order.Items, item => item.ProductName == "Snapshot Admission"
            && item.Quantity == 2 && item.UnitPrice == 10m && item.LineTotal == 20m);

        var filtered = await _officerClient.GetFromJsonAsync<List<OrderDto>>("/api/orders?status=Paid");
        Assert.NotNull(filtered);
        var paidOrder = Assert.Single(filtered);
        Assert.Equal(_paidOrderId, paidOrder.OrderId);
        Assert.Null(paidOrder.CustomerName);

        using var invalidFilter = await _officerClient.GetAsync("/api/orders?status=Unknown");
        Assert.Equal(HttpStatusCode.BadRequest, invalidFilter.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_updates_allowed_transition_and_returns_new_order()
    {
        var before = await GetOrderAsync(_orderId);

        using var response = await _officerClient.PutAsJsonAsync(
            $"/api/orders/{_orderId}/status",
            new ChangeOrderStatusRequest(OrderStatuses.Paid, before.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(updated);
        Assert.Equal(OrderStatuses.Paid, updated.Status);
        Assert.Equal(_orderId, updated.OrderId);
    }

    [Fact]
    public async Task ChangeStatus_allows_paid_order_to_be_picked_up()
    {
        var before = await GetOrderAsync(_paidOrderId);

        using var response = await _officerClient.PutAsJsonAsync(
            $"/api/orders/{_paidOrderId}/status",
            new ChangeOrderStatusRequest(OrderStatuses.PickedUp, before.RowVersion));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(updated);
        Assert.Equal(OrderStatuses.PickedUp, updated.Status);
    }

    [Fact]
    public async Task ChangeStatus_returns_conflict_for_illegal_transition_and_preserves_order()
    {
        var before = await GetOrderAsync(_orderId);

        using var response = await _officerClient.PutAsJsonAsync(
            $"/api/orders/{_orderId}/status",
            new ChangeOrderStatusRequest(OrderStatuses.PickedUp, before.RowVersion));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var saved = await GetOrderAsync(_orderId);
        Assert.Equal(OrderStatuses.Pending, saved.Status);
    }

    [Fact]
    public async Task ChangeStatus_returns_conflict_for_stale_version()
    {
        var before = await GetOrderAsync(_orderId);

        using var response = await _officerClient.PutAsJsonAsync(
            $"/api/orders/{_orderId}/status",
            new ChangeOrderStatusRequest(OrderStatuses.Paid, before.RowVersion + 1));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(OrderStatuses.Pending, (await GetOrderAsync(_orderId)).Status);
    }

    [Fact]
    public async Task ChangeStatus_returns_bad_request_for_unknown_status_or_missing_version()
    {
        using var unknownStatus = await _officerClient.PutAsJsonAsync(
            $"/api/orders/{_orderId}/status",
            new ChangeOrderStatusRequest("Refunded", 0));
        Assert.Equal(HttpStatusCode.BadRequest, unknownStatus.StatusCode);

        using var missingVersion = await _officerClient.PutAsJsonAsync(
            $"/api/orders/{_orderId}/status",
            new ChangeOrderStatusRequest(OrderStatuses.Paid, null));
        Assert.Equal(HttpStatusCode.BadRequest, missingVersion.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_returns_not_found_for_unknown_order()
    {
        using var response = await _officerClient.PutAsJsonAsync(
            "/api/orders/999999/status",
            new ChangeOrderStatusRequest(OrderStatuses.Paid, 0));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_cancellation_restores_merchandise_and_seats_once()
    {
        var before = await GetOrderAsync(_orderId);

        using var first = await _officerClient.PutAsJsonAsync(
            $"/api/orders/{_orderId}/status",
            new ChangeOrderStatusRequest(OrderStatuses.Canceled, before.RowVersion));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(8, await LoadMerchandiseStockAsync());
        Assert.Equal(0, await LoadTicketsSoldAsync());

        using var second = await _officerClient.PutAsJsonAsync(
            $"/api/orders/{_orderId}/status",
            new ChangeOrderStatusRequest(OrderStatuses.Canceled, before.RowVersion));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(8, await LoadMerchandiseStockAsync());
        Assert.Equal(0, await LoadTicketsSoldAsync());
    }

    [Fact]
    public async Task Officer_policy_returns_unauthorized_to_anonymous_list_and_status_requests()
    {
        using var anonymous = _testFactory.CreateClient();

        using var list = await anonymous.GetAsync("/api/orders");
        using var change = await anonymous.PutAsJsonAsync(
            $"/api/orders/{_orderId}/status",
            new ChangeOrderStatusRequest(OrderStatuses.Paid, 0));

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, change.StatusCode);
    }

    [Fact]
    public async Task Officer_policy_returns_forbidden_to_member_on_list_and_status_requests()
    {
        using var member = _testFactory.CreateClient();
        await TestAccounts.SignInAsMemberAsync(_testFactory, member);

        using var list = await member.GetAsync("/api/orders");
        using var change = await member.PutAsJsonAsync(
            $"/api/orders/{_orderId}/status",
            new ChangeOrderStatusRequest(OrderStatuses.Paid, 0));

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, change.StatusCode);
    }

    private async Task SeedOrdersAsync(AppDbContext db)
    {
        var customer = new Customer { FirstName = "Alex", LastName = "Customer" };
        var venue = new Venue { Name = "Orders API Venue", Capacity = 50 };
        var startsAt = DateTime.UtcNow.AddDays(1);
        var @event = new Event
        {
            Venue = venue,
            Name = "Orders API Event",
            StartsAtUtc = startsAt,
            EndsAtUtc = startsAt.AddHours(1),
            TicketCapacity = 10,
            TicketsSold = 2,
        };
        var merchandise = new Product
        {
            Category = "Apparel",
            Name = "Snapshot Shirt",
            Price = 20,
            MemberPrice = 15,
            StockQuantity = 5,
        };
        var ticketType = new TicketType
        {
            Category = "Tickets",
            Name = "Snapshot Admission",
            Price = 10,
            MemberPrice = 8,
            Event = @event,
        };
        _orderDateUtc = new DateTime(2026, 11, 20, 17, 0, 0, DateTimeKind.Utc);
        var order = new Order
        {
            Customer = customer,
            OrderDate = _orderDateUtc,
            Status = OrderStatuses.Pending,
            TotalAmount = 50,
        };
        var paidOrder = new Order
        {
            OrderDate = _orderDateUtc.AddMinutes(-5),
            Status = OrderStatuses.Paid,
            TotalAmount = 0,
        };

        db.Orders.AddRange(order, paidOrder);
        db.Products.AddRange(merchandise, ticketType);
        await db.SaveChangesAsync();

        db.OrderLines.AddRange(
            new OrderLine
            {
                Order = order,
                ProductId = merchandise.ProductId,
                Quantity = 3,
                UnitPrice = 10,
                ProductNameSnapshot = "Snapshot Shirt",
            },
            new OrderLine
            {
                Order = order,
                ProductId = ticketType.ProductId,
                Quantity = 2,
                UnitPrice = 10,
                ProductNameSnapshot = "Snapshot Admission",
            });
        await db.SaveChangesAsync();

        var ticketLine = await db.OrderLines.SingleAsync(line =>
            line.OrderId == order.OrderId && line.ProductId == ticketType.ProductId);
        db.TicketRegistrations.AddRange(
            new TicketRegistration
            {
                Event = @event,
                TicketTypeId = ticketType.ProductId,
                OrderLine = ticketLine,
                CreatedAtUtc = _orderDateUtc,
            },
            new TicketRegistration
            {
                Event = @event,
                TicketTypeId = ticketType.ProductId,
                OrderLine = ticketLine,
                CreatedAtUtc = _orderDateUtc,
            });
        await db.SaveChangesAsync();

        _orderId = order.OrderId;
        _paidOrderId = paidOrder.OrderId;
        _merchandiseId = merchandise.ProductId;
        _eventId = @event.EventId;
    }

    private async Task<OrderDto> GetOrderAsync(int orderId)
    {
        var orders = await _officerClient.GetFromJsonAsync<List<OrderDto>>("/api/orders");
        return orders?.Single(order => order.OrderId == orderId)
            ?? throw new InvalidOperationException($"Order {orderId} was not returned by the API.");
    }

    private async Task<int> LoadMerchandiseStockAsync()
    {
        using var scope = _testFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(product => product.ProductId == _merchandiseId)
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
}