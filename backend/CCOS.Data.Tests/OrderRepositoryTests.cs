using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Data.Tests;

public sealed class OrderRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private OrderRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _repository = new OrderRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Theory]
    [InlineData(OrderStatuses.Pending, OrderStatuses.Paid)]
    [InlineData(OrderStatuses.Paid, OrderStatuses.PickedUp)]
    public async Task ChangeStatus_saves_each_allowed_non_cancel_transition(string currentStatus, string nextStatus)
    {
        var seeded = await AddOrderAsync(currentStatus);

        var result = await _repository.ChangeStatusAsync(
            seeded.Order.OrderId,
            nextStatus,
            seeded.Order.RowVersion);

        Assert.Equal(OrderStatusChangeOutcome.Updated, result.Outcome);
        Assert.Equal(nextStatus, (await LoadOrderAsync(seeded.Order.OrderId)).Status);
        Assert.Equal(5, await LoadMerchandiseStockAsync(seeded.Merchandise.ProductId));
        Assert.Equal(2, await LoadTicketsSoldAsync(seeded.Event.EventId));
    }

    [Theory]
    [InlineData(OrderStatuses.Pending)]
    [InlineData(OrderStatuses.Paid)]
    public async Task Cancel_returns_merchandise_and_event_seats_once_even_when_deactivated(string currentStatus)
    {
        var seeded = await AddOrderAsync(currentStatus, merchandiseActive: false, eventActive: false);

        var first = await _repository.ChangeStatusAsync(
            seeded.Order.OrderId,
            OrderStatuses.Canceled,
            seeded.Order.RowVersion);

        Assert.Equal(OrderStatusChangeOutcome.Updated, first.Outcome);
        Assert.Equal(OrderStatuses.Canceled, (await LoadOrderAsync(seeded.Order.OrderId)).Status);
        Assert.Equal(8, await LoadMerchandiseStockAsync(seeded.Merchandise.ProductId));
        Assert.Equal(0, await LoadTicketsSoldAsync(seeded.Event.EventId));
        Assert.All(
            await _db.TicketRegistrations.AsNoTracking().ToListAsync(),
            registration => Assert.Equal(RegistrationStatus.Canceled, registration.Status));

        var second = await _repository.ChangeStatusAsync(
            seeded.Order.OrderId,
            OrderStatuses.Canceled,
            seeded.Order.RowVersion);

        Assert.Equal(OrderStatusChangeOutcome.InvalidTransition, second.Outcome);
        Assert.Equal(8, await LoadMerchandiseStockAsync(seeded.Merchandise.ProductId));
        Assert.Equal(0, await LoadTicketsSoldAsync(seeded.Event.EventId));
    }

    [Theory]
    [InlineData(OrderStatuses.Canceled, OrderStatuses.Paid)]
    [InlineData(OrderStatuses.PickedUp, OrderStatuses.Canceled)]
    [InlineData(OrderStatuses.Pending, OrderStatuses.PickedUp)]
    public async Task ChangeStatus_rejects_illegal_transition_without_changing_order_or_inventory(
        string currentStatus,
        string nextStatus)
    {
        var seeded = await AddOrderAsync(currentStatus);

        var result = await _repository.ChangeStatusAsync(
            seeded.Order.OrderId,
            nextStatus,
            seeded.Order.RowVersion);

        Assert.Equal(OrderStatusChangeOutcome.InvalidTransition, result.Outcome);
        Assert.Equal(currentStatus, (await LoadOrderAsync(seeded.Order.OrderId)).Status);
        Assert.Equal(5, await LoadMerchandiseStockAsync(seeded.Merchandise.ProductId));
        Assert.Equal(2, await LoadTicketsSoldAsync(seeded.Event.EventId));
        Assert.All(
            await _db.TicketRegistrations.AsNoTracking().ToListAsync(),
            registration => Assert.Equal(RegistrationStatus.Purchased, registration.Status));
    }

    [Fact]
    public async Task ChangeStatus_rejects_stale_version_without_changing_order_or_inventory()
    {
        var seeded = await AddOrderAsync();

        var result = await _repository.ChangeStatusAsync(
            seeded.Order.OrderId,
            OrderStatuses.Canceled,
            expectedRowVersion: seeded.Order.RowVersion + 1);

        Assert.Equal(OrderStatusChangeOutcome.Conflict, result.Outcome);
        Assert.Equal(OrderStatuses.Pending, (await LoadOrderAsync(seeded.Order.OrderId)).Status);
        Assert.Equal(5, await LoadMerchandiseStockAsync(seeded.Merchandise.ProductId));
        Assert.Equal(2, await LoadTicketsSoldAsync(seeded.Event.EventId));
    }

    [Fact]
    public async Task ChangeStatus_returns_not_found_for_unknown_order()
    {
        var result = await _repository.ChangeStatusAsync(999, OrderStatuses.Paid, expectedRowVersion: 0);

        Assert.Equal(OrderStatusChangeOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Cancel_rolls_back_status_stock_and_ticket_changes_when_seat_counts_are_inconsistent()
    {
        var seeded = await AddOrderAsync(ticketsSold: 1);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.ChangeStatusAsync(
            seeded.Order.OrderId,
            OrderStatuses.Canceled,
            seeded.Order.RowVersion));

        Assert.Contains("fewer sold tickets", error.Message);
        Assert.Equal(OrderStatuses.Pending, (await LoadOrderAsync(seeded.Order.OrderId)).Status);
        Assert.Equal(5, await LoadMerchandiseStockAsync(seeded.Merchandise.ProductId));
        Assert.Equal(1, await LoadTicketsSoldAsync(seeded.Event.EventId));
        Assert.All(
            await _db.TicketRegistrations.AsNoTracking().ToListAsync(),
            registration => Assert.Equal(RegistrationStatus.Purchased, registration.Status));
    }

    private async Task<(Order Order, Product Merchandise, Event Event)> AddOrderAsync(
        string status = OrderStatuses.Pending,
        bool merchandiseActive = true,
        bool eventActive = true,
        int ticketsSold = 2)
    {
        var venue = new Venue { Name = "Order test venue", Capacity = 20 };
        var startsAt = DateTime.UtcNow.AddDays(1);
        var @event = new Event
        {
            Venue = venue,
            Name = "Order test event",
            StartsAtUtc = startsAt,
            EndsAtUtc = startsAt.AddHours(1),
            TicketCapacity = 10,
            TicketsSold = ticketsSold,
            IsActive = eventActive,
        };
        var merchandise = new Product
        {
            Category = "Apparel",
            Name = "Order test shirt",
            Price = 10,
            MemberPrice = 8,
            StockQuantity = 5,
            IsActive = merchandiseActive,
        };
        var ticketType = new TicketType
        {
            Category = "Tickets",
            Name = "General admission",
            Price = 10,
            MemberPrice = 8,
            Event = @event,
        };
        var order = new Order
        {
            OrderDate = DateTime.UtcNow,
            Status = status,
            TotalAmount = 50,
        };

        _db.Orders.Add(order);
        _db.Products.AddRange(merchandise, ticketType);
        await _db.SaveChangesAsync();

        var merchandiseLine = new OrderLine
        {
            Order = order,
            ProductId = merchandise.ProductId,
            Quantity = 3,
            UnitPrice = 10,
            ProductNameSnapshot = merchandise.Name,
        };
        var ticketLine = new OrderLine
        {
            Order = order,
            ProductId = ticketType.ProductId,
            Quantity = 2,
            UnitPrice = 10,
            ProductNameSnapshot = ticketType.Name,
        };
        _db.OrderLines.AddRange(merchandiseLine, ticketLine);
        await _db.SaveChangesAsync();

        _db.TicketRegistrations.AddRange(
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
        await _db.SaveChangesAsync();

        return (order, merchandise, @event);
    }

    private Task<Order> LoadOrderAsync(int orderId) =>
        _db.Orders.AsNoTracking().SingleAsync(order => order.OrderId == orderId);

    private Task<int> LoadMerchandiseStockAsync(int productId) =>
        _db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(product => product.ProductId == productId)
            .Select(product => product.StockQuantity)
            .SingleAsync();

    private Task<int> LoadTicketsSoldAsync(int eventId) =>
        _db.Events.AsNoTracking()
            .Where(@event => @event.EventId == eventId)
            .Select(@event => @event.TicketsSold)
            .SingleAsync();
}
