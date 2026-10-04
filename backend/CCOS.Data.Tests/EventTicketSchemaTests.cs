using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CCOS.Data.Tests;

public class EventTicketSchemaTests
{
    [Fact]
    public void Utc_converter_normalizes_local_values_before_provider_write()
    {
        var converter = new UtcDateTimeConverter();
        var localValue = new DateTime(2026, 10, 3, 19, 0, 0, DateTimeKind.Local);

        var providerValue = (DateTime)converter.ConvertToProvider(localValue)!;

        Assert.Equal(localValue.ToUniversalTime(), providerValue);
        Assert.Equal(DateTimeKind.Utc, providerValue.Kind);
    }

    [Fact]
    public async Task Model_builds_with_expected_constraints_and_restrict_relationships()
    {
        await using var connection = await CreateConnectionAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AppDbContext(options);
        var model = db.GetService<IDesignTimeModel>().Model;
        var eventEntity = model.FindEntityType(typeof(Event))!;
        var registrationEntity = model.FindEntityType(typeof(TicketRegistration))!;
        var productEntity = model.FindEntityType(typeof(Product))!;
        var venueEntity = model.FindEntityType(typeof(Venue))!;

        Assert.Empty(eventEntity.GetDeclaredQueryFilters());
        Assert.Equal("datetime2(0)", eventEntity.FindProperty(nameof(Event.StartsAtUtc))!.GetColumnType());
        Assert.Equal(
            ["CK_Events_TicketCapacity", "CK_Events_TicketsSold", "CK_Events_Times"],
            eventEntity.GetCheckConstraints().Select(constraint => constraint.Name!).Order().ToArray());
        Assert.Contains(
            "CK_TicketRegistrations_Status",
            registrationEntity.GetCheckConstraints().Select(constraint => constraint.Name));
        Assert.Contains("CK_Venues_Capacity", venueEntity.GetCheckConstraints().Select(constraint => constraint.Name));
        Assert.Equal(DeleteBehavior.Restrict, registrationEntity.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Event))
            .DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, registrationEntity.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(TicketType))
            .DeleteBehavior);
        Assert.Contains(
            "CK_Products_TicketEvent",
            productEntity.GetCheckConstraints().Select(constraint => constraint.Name));
        Assert.Equal("ProductType", productEntity.FindDiscriminatorProperty()!.Name);
        Assert.NotEmpty(productEntity.GetDeclaredQueryFilters());
        Assert.Null(typeof(TicketRegistration).GetProperty("TicketType"));
        Assert.All(
            model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()),
            foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        Assert.Equal(false, eventEntity.FindProperty(nameof(Event.MembersOnly))!.GetDefaultValue());
        Assert.Equal(true, productEntity.FindProperty(nameof(Product.IsActive))!.GetDefaultValue());
    }

    [Fact]
    public void SqlServer_model_uses_rowversion_and_expected_indexes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=CCOS;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var db = new AppDbContext(options);
        var model = db.GetService<IDesignTimeModel>().Model;
        var eventEntity = model.FindEntityType(typeof(Event))!;
        var registrationEntity = model.FindEntityType(typeof(TicketRegistration))!;
        var rowVersion = eventEntity.FindProperty(nameof(Event.RowVersion))!;
        var startsAtIndex = eventEntity.GetIndexes().Single(index => index.GetDatabaseName() == "IX_Events_StartsAtUtc");
        var venueIndex = eventEntity.GetIndexes().Single(index => index.GetDatabaseName() == "IX_Events_VenueId_StartsAtUtc");

        Assert.Equal("rowversion", rowVersion.GetColumnType());
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
        Assert.Equal("[IsActive] = 1", startsAtIndex.GetFilter());
        Assert.Equal(
            new[] { "EndsAtUtc", "Name", "VenueId", "MembersOnly" },
            startsAtIndex.FindAnnotation("SqlServer:Include")!.Value);
        Assert.Equal(
            new[] { "EndsAtUtc", "IsActive" },
            venueIndex.FindAnnotation("SqlServer:Include")!.Value);
        Assert.Equal(
            new[] { "EventId", "Status" },
            registrationEntity.GetIndexes().Single(index =>
                index.GetDatabaseName() == "IX_TicketRegistrations_EventId_Status")
                .Properties.Select(property => property.Name));
        Assert.Equal("[EventId] IS NOT NULL", model.FindEntityType(typeof(TicketType))!.GetIndexes()
            .Single(index => index.GetDatabaseName() == "IX_Products_EventId")
            .GetFilter());
        Assert.True(model.FindEntityType(typeof(Venue))!.GetIndexes()
            .Single(index => index.GetDatabaseName() == "UX_Venues_Name")
            .IsUnique);
    }

    [Fact]
    public async Task Reservation_blocks_overselling()
    {
        await using var connection = await CreateConnectionAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var venue = new Venue { Name = "Test venue", Capacity = 10 };
        db.Venues.Add(venue);
        var @event = CreateEvent(venue, ticketCapacity: 2);
        db.Events.Add(@event);
        await db.SaveChangesAsync();

        var repository = new TicketBookingRepository(db);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await repository.ReserveSeatsAsync(@event.EventId, 2);
        var exception = await Assert.ThrowsAsync<SoldOutException>(() => repository.ReserveSeatsAsync(@event.EventId, 1));
        Assert.Contains("unavailable", exception.Message);
        await transaction.CommitAsync();

        var savedEvent = await db.Events.AsNoTracking().SingleAsync();
        Assert.Equal(2, savedEvent.TicketsSold);
        Assert.Equal(DateTimeKind.Utc, savedEvent.StartsAtUtc.Kind);
    }

    [Fact]
    public async Task Reservation_rejects_invalid_quantities_before_grouping()
    {
        await using var connection = await CreateConnectionAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var venue = new Venue { Name = "Test venue", Capacity = 10 };
        var @event = CreateEvent(venue, ticketCapacity: 10);
        db.Venues.Add(venue);
        db.Events.Add(@event);
        await db.SaveChangesAsync();

        var repository = new TicketBookingRepository(db);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.ReserveTicketsForOrderAsync(
            [
                new EventTicketReservation(@event.EventId, 5),
                new EventTicketReservation(@event.EventId, -4)
            ]));
        await transaction.CommitAsync();

        Assert.Equal(0, (await db.Events.AsNoTracking().SingleAsync()).TicketsSold);
    }

    [Fact]
    public async Task Cancellation_marks_registrations_and_decrements_event_count()
    {
        await using var connection = await CreateConnectionAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var venue = new Venue { Name = "Test venue", Capacity = 10 };
        var @event = CreateEvent(venue, ticketCapacity: 3, ticketsSold: 2);
        var ticketType = new TicketType
        {
            Name = "General",
            Category = "Tickets",
            Price = 10,
            MemberPrice = 10,
            StockQuantity = 0,
            Event = @event
        };
        var order = new Order { OrderDate = DateTime.UtcNow, TotalAmount = 20 };
        db.Venues.Add(venue);
        db.Events.Add(@event);
        db.TicketTypes.Add(ticketType);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var orderLine = new OrderLine
        {
            Order = order,
            ProductId = ticketType.ProductId,
            Quantity = 2,
            UnitPrice = 10
        };
        db.OrderLines.Add(orderLine);
        await db.SaveChangesAsync();
        db.TicketRegistrations.AddRange(
            new TicketRegistration
            {
                Event = @event,
                TicketTypeId = ticketType.ProductId,
                OrderLine = orderLine,
                CreatedAtUtc = DateTime.UtcNow
            },
            new TicketRegistration
            {
                Event = @event,
                TicketTypeId = ticketType.ProductId,
                OrderLine = orderLine,
                CreatedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var repository = new TicketBookingRepository(db);
        var canceledAtUtc = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);
        var canceledCount = await repository.CancelOrderRegistrationsAsync(order.OrderId, canceledAtUtc);
        var canceledAgain = await repository.CancelOrderRegistrationsAsync(order.OrderId, canceledAtUtc);

        Assert.Equal(2, canceledCount);
        Assert.Equal(0, canceledAgain);
        Assert.Equal(0, (await db.Events.AsNoTracking().SingleAsync()).TicketsSold);
        var registrations = await db.TicketRegistrations.AsNoTracking().ToListAsync();
        Assert.All(registrations, registration =>
        {
            Assert.Equal(RegistrationStatus.Canceled, registration.Status);
            Assert.Equal(DateTimeKind.Utc, registration.CanceledAtUtc!.Value.Kind);
            Assert.Equal(DateTimeKind.Utc, registration.CreatedAtUtc.Kind);
        });
    }

    private static Event CreateEvent(Venue venue, int ticketCapacity, int ticketsSold = 0)
    {
        var startsAtUtc = DateTime.UtcNow.AddDays(1);
        return new Event
        {
            Venue = venue,
            Name = "Test event",
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = startsAtUtc.AddHours(1),
            TicketCapacity = ticketCapacity,
            TicketsSold = ticketsSold
        };
    }

    private static async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }
}
