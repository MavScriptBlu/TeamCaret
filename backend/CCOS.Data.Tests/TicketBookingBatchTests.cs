using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Data.Tests;

/// <summary>
/// Tests for the batched ticket updates in TicketBookingRepository: an order with several
/// events reserves and cancels with one UPDATE for all of them (performance batch). These check
/// the same promises as before: all or nothing, no overselling, and canceling twice is harmless.
/// </summary>
public sealed class TicketBookingBatchTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private TicketBookingRepository _repository = null!;
    private Venue _venue = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        _venue = new Venue { Name = "Test venue", Capacity = 100 };
        _db.Venues.Add(_venue);
        await _db.SaveChangesAsync();

        _repository = new TicketBookingRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<Event> AddEventAsync(int capacity, int sold = 0, bool isActive = true)
    {
        var start = DateTime.UtcNow.AddDays(1);
        var @event = new Event
        {
            Venue = _venue,
            Name = "Event",
            StartsAtUtc = start,
            EndsAtUtc = start.AddHours(1),
            TicketCapacity = capacity,
            TicketsSold = sold,
            IsActive = isActive,
        };
        _db.Events.Add(@event);
        await _db.SaveChangesAsync();
        return @event;
    }

    private async Task<int> SoldAsync(Event @event) =>
        (await _db.Events.AsNoTracking().SingleAsync(candidate => candidate.EventId == @event.EventId)).TicketsSold;

    // ---- reserving ----

    [Fact]
    public async Task Reserve_for_several_events_updates_each_with_its_own_amount()
    {
        var first = await AddEventAsync(capacity: 10);
        var second = await AddEventAsync(capacity: 10, sold: 3);
        var third = await AddEventAsync(capacity: 5);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _repository.ReserveTicketsForOrderAsync(
        [
            new EventTicketReservation(third.EventId, 5),
            new EventTicketReservation(first.EventId, 2),
            new EventTicketReservation(second.EventId, 4),
        ]);
        await transaction.CommitAsync();

        Assert.Equal(2, await SoldAsync(first));
        Assert.Equal(7, await SoldAsync(second));
        Assert.Equal(5, await SoldAsync(third));
    }

    [Fact]
    public async Task Reserve_adds_up_lines_for_the_same_event()
    {
        var @event = await AddEventAsync(capacity: 10);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _repository.ReserveTicketsForOrderAsync(
        [
            new EventTicketReservation(@event.EventId, 2),
            new EventTicketReservation(@event.EventId, 3),
        ]);
        await transaction.CommitAsync();

        Assert.Equal(5, await SoldAsync(@event));
    }

    [Fact]
    public async Task Reserve_changes_nothing_when_one_event_is_full_and_names_that_event()
    {
        var roomy = await AddEventAsync(capacity: 10);
        var full = await AddEventAsync(capacity: 3, sold: 3);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var error = await Assert.ThrowsAsync<SoldOutException>(() => _repository.ReserveTicketsForOrderAsync(
        [
            new EventTicketReservation(roomy.EventId, 2),
            new EventTicketReservation(full.EventId, 1),
        ]));

        Assert.Equal(full.EventId, error.EventId);

        // All or nothing: the event with room wasn't touched either.
        Assert.Equal(0, await SoldAsync(roomy));
        Assert.Equal(3, await SoldAsync(full));
    }

    [Fact]
    public async Task Reserve_refuses_more_seats_than_are_left_even_by_one()
    {
        var @event = await AddEventAsync(capacity: 5, sold: 4);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<SoldOutException>(() => _repository.ReserveSeatsAsync(@event.EventId, 2));

        Assert.Equal(4, await SoldAsync(@event));
    }

    [Fact]
    public async Task Reserve_allows_filling_an_event_exactly()
    {
        var @event = await AddEventAsync(capacity: 5, sold: 3);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _repository.ReserveSeatsAsync(@event.EventId, 2);
        await transaction.CommitAsync();

        Assert.Equal(5, await SoldAsync(@event));
    }

    [Fact]
    public async Task Reserve_treats_an_inactive_event_as_sold_out()
    {
        var removed = await AddEventAsync(capacity: 10, isActive: false);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var error = await Assert.ThrowsAsync<SoldOutException>(() => _repository.ReserveSeatsAsync(removed.EventId, 1));

        Assert.Equal(removed.EventId, error.EventId);
    }

    [Fact]
    public async Task Reserve_treats_a_missing_event_as_sold_out()
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var error = await Assert.ThrowsAsync<SoldOutException>(() => _repository.ReserveSeatsAsync(99999, 1));

        Assert.Equal(99999, error.EventId);
    }

    [Fact]
    public async Task Reserve_does_not_overflow_on_a_huge_quantity()
    {
        var @event = await AddEventAsync(capacity: 10, sold: 5);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<SoldOutException>(() => _repository.ReserveSeatsAsync(@event.EventId, int.MaxValue));

        Assert.Equal(5, await SoldAsync(@event));
    }

    [Fact]
    public async Task Reserve_still_needs_an_open_transaction()
    {
        var @event = await AddEventAsync(capacity: 10);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.ReserveSeatsAsync(@event.EventId, 1));
    }

    // ---- canceling ----

    // An order with the given number of purchased tickets per event.
    private async Task<Order> AddOrderAsync(params (Event Event, int Tickets)[] lines)
    {
        var order = new Order { OrderDate = DateTime.UtcNow, TotalAmount = 10 };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        foreach (var (@event, tickets) in lines)
        {
            var ticketType = new TicketType
            {
                Name = "General",
                Category = "Tickets",
                Price = 10,
                MemberPrice = 10,
                Event = @event,
            };
            _db.TicketTypes.Add(ticketType);
            await _db.SaveChangesAsync();

            var orderLine = new OrderLine
            {
                Order = order,
                ProductId = ticketType.ProductId,
                Quantity = tickets,
                UnitPrice = 10,
                ProductNameSnapshot = "General",
            };
            _db.OrderLines.Add(orderLine);
            await _db.SaveChangesAsync();

            for (var ticket = 0; ticket < tickets; ticket++)
            {
                _db.TicketRegistrations.Add(new TicketRegistration
                {
                    Event = @event,
                    TicketTypeId = ticketType.ProductId,
                    OrderLine = orderLine,
                    CreatedAtUtc = DateTime.UtcNow,
                });
            }

            await _db.SaveChangesAsync();
        }

        return order;
    }

    [Fact]
    public async Task Cancel_gives_seats_back_to_each_event_in_one_go()
    {
        var first = await AddEventAsync(capacity: 10, sold: 5);
        var second = await AddEventAsync(capacity: 10, sold: 2);
        var order = await AddOrderAsync((first, 3), (second, 2));

        var canceled = await _repository.CancelOrderRegistrationsAsync(order.OrderId);

        Assert.Equal(5, canceled);
        Assert.Equal(2, await SoldAsync(first));
        Assert.Equal(0, await SoldAsync(second));
        Assert.All(
            await _db.TicketRegistrations.AsNoTracking().ToListAsync(),
            registration => Assert.Equal(RegistrationStatus.Canceled, registration.Status));
    }

    [Fact]
    public async Task Cancel_twice_only_gives_the_seats_back_once()
    {
        var @event = await AddEventAsync(capacity: 10, sold: 4);
        var order = await AddOrderAsync((@event, 2));

        var first = await _repository.CancelOrderRegistrationsAsync(order.OrderId);
        var second = await _repository.CancelOrderRegistrationsAsync(order.OrderId);

        Assert.Equal(2, first);
        Assert.Equal(0, second);
        Assert.Equal(2, await SoldAsync(@event));
    }

    [Fact]
    public async Task Cancel_returns_zero_for_an_order_with_no_tickets()
    {
        var order = await AddOrderAsync();

        Assert.Equal(0, await _repository.CancelOrderRegistrationsAsync(order.OrderId));
    }

    [Fact]
    public async Task Cancel_changes_nothing_when_the_event_counts_are_out_of_sync()
    {
        // The event says 1 ticket is sold, but the order has 3 purchased: the numbers don't add up.
        var @event = await AddEventAsync(capacity: 10, sold: 1);
        var order = await AddOrderAsync((@event, 3));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repository.CancelOrderRegistrationsAsync(order.OrderId));

        // The whole cancel was rolled back: tickets are still purchased and the count is unchanged.
        Assert.Equal(1, await SoldAsync(@event));
        Assert.All(
            await _db.TicketRegistrations.AsNoTracking().ToListAsync(),
            registration => Assert.Equal(RegistrationStatus.Purchased, registration.Status));
    }
}
