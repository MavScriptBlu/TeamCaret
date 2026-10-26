using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CCOS.Data.Tests;

/// <summary>
/// Tests for the event and ticket tables (CAR-30). Two kinds of test live here:
/// "model" tests that only check how AppDbContext is set up, and "behavior" tests that
/// run the booking code against a throwaway in-memory SQLite database.
/// </summary>
public class EventTicketSchemaTests
{
    // Expected column lists, built once so the tests do not rebuild the same array on every call.
    private static readonly string[] StartsAtIndexIncludedColumns = ["EndsAtUtc", "Name", "VenueId", "MembersOnly"];
    private static readonly string[] VenueIndexIncludedColumns = ["EndsAtUtc", "IsActive"];
    private static readonly string[] RegistrationIndexColumns = ["EventId", "Status"];

    /// <summary>
    /// Checks the rules written in AppDbContext: the check constraints exist, deleting is
    /// blocked (Restrict) on every relationship, and the defaults are right.
    /// </summary>
    [Fact]
    public async Task Model_builds_with_expected_constraints_and_restrict_relationships()
    {
        // Arrange: open a temporary in-memory database and build the model from it.
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

        // Events are never hidden by a query filter (only products are).
        Assert.Empty(eventEntity.GetDeclaredQueryFilters());

        // The check constraints the database will enforce.
        Assert.Equal(
            ["CK_Events_TicketCapacity", "CK_Events_TicketsSold", "CK_Events_Times"],
            eventEntity.GetCheckConstraints().Select(constraint => constraint.Name!).Order().ToArray());
        Assert.Contains(
            "CK_TicketRegistrations_Status",
            registrationEntity.GetCheckConstraints().Select(constraint => constraint.Name));
        Assert.Contains("CK_Venues_Capacity", venueEntity.GetCheckConstraints().Select(constraint => constraint.Name));

        // A registration blocks deleting the event or ticket type it points at.
        Assert.Equal(DeleteBehavior.Restrict, registrationEntity.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Event))
            .DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, registrationEntity.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(TicketType))
            .DeleteBehavior);

        // Products and ticket types share one table, told apart by the ProductType column.
        Assert.Contains(
            "CK_Products_TicketEvent",
            productEntity.GetCheckConstraints().Select(constraint => constraint.Name));
        Assert.Equal("ProductType", productEntity.FindDiscriminatorProperty()!.Name);
        Assert.NotEmpty(productEntity.GetDeclaredQueryFilters());
        Assert.Null(typeof(TicketRegistration).GetProperty("TicketType"));

        // Every relationship in the whole model blocks deletes, so no data disappears by accident.
        Assert.All(
            model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()),
            foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));

        // Defaults: events are open to everyone and products start active.
        Assert.Equal(false, eventEntity.FindProperty(nameof(Event.MembersOnly))!.GetDefaultValue());
        Assert.Equal(true, productEntity.FindProperty(nameof(Product.IsActive))!.GetDefaultValue());
    }

    /// <summary>
    /// Checks the PostgreSQL-only setup: the xmin concurrency column, the time column type,
    /// and the indexes. Only builds the model, so it needs no running database.
    /// </summary>
    [Fact]
    public void Postgres_model_uses_xmin_and_expected_indexes()
    {
        // Only builds the model. It never connects, so no PostgreSQL server is needed to run this test.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=ccos;Username=ccos;Password=not-used")
            .Options;

        using var db = new AppDbContext(options);
        var model = db.GetService<IDesignTimeModel>().Model;
        var eventEntity = model.FindEntityType(typeof(Event))!;
        var registrationEntity = model.FindEntityType(typeof(TicketRegistration))!;
        var rowVersion = eventEntity.FindProperty(nameof(Event.RowVersion))!;
        var startsAtIndex = eventEntity.GetIndexes().Single(index => index.GetDatabaseName() == "IX_Events_StartsAtUtc");
        var venueIndex = eventEntity.GetIndexes().Single(index => index.GetDatabaseName() == "IX_Events_VenueId_StartsAtUtc");

        // xmin is PostgreSQL's hidden per-row change counter. It is what blocks overselling (CAR-30).
        Assert.Equal("xmin", rowVersion.GetColumnName());
        Assert.Equal("xid", rowVersion.GetColumnType());
        Assert.Equal("timestamp(0) with time zone", eventEntity.FindProperty(nameof(Event.StartsAtUtc))!.GetColumnType());
        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);

        // The upcoming-events index only covers active events and carries extra columns for list screens.
        Assert.Equal("\"IsActive\" = true", startsAtIndex.GetFilter());
        Assert.Equal(
            StartsAtIndexIncludedColumns,
            startsAtIndex.FindAnnotation("Npgsql:IndexInclude")!.Value);
        Assert.Equal(
            VenueIndexIncludedColumns,
            venueIndex.FindAnnotation("Npgsql:IndexInclude")!.Value);

        // An event must end after it starts.
        Assert.Equal(
            "\"EndsAtUtc\" > \"StartsAtUtc\"",
            eventEntity.GetCheckConstraints().Single(constraint => constraint.Name == "CK_Events_Times").Sql);
        Assert.Equal("now()", model.FindEntityType(typeof(TicketRegistration))!
            .FindProperty(nameof(TicketRegistration.CreatedAtUtc))!.GetDefaultValueSql());
        Assert.Equal(
            RegistrationIndexColumns,
            registrationEntity.GetIndexes().Single(index =>
                index.GetDatabaseName() == "IX_TicketRegistrations_EventId_Status")
                .Properties.Select(property => property.Name));

        // Only ticket rows (which have an EventId) are indexed by event, and venue names are unique.
        Assert.Equal("\"EventId\" IS NOT NULL", model.FindEntityType(typeof(TicketType))!.GetIndexes()
            .Single(index => index.GetDatabaseName() == "IX_Products_EventId")
            .GetFilter());
        Assert.True(model.FindEntityType(typeof(Venue))!.GetIndexes()
            .Single(index => index.GetDatabaseName() == "UX_Venues_Name")
            .IsUnique);
    }

    /// <summary>
    /// An event with 2 seats: reserving 2 works, reserving 1 more must fail with SoldOutException,
    /// and the saved count stays at 2.
    /// </summary>
    [Fact]
    public async Task Reservation_blocks_overselling()
    {
        // Arrange: a fresh database with one venue and an event that holds 2 tickets.
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

        // Act: reserving seats must happen inside a transaction, like a real order would.
        var repository = new TicketBookingRepository(db);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await repository.ReserveSeatsAsync(@event.EventId, 2);
        await Assert.ThrowsAsync<SoldOutException>(() => repository.ReserveSeatsAsync(@event.EventId, 1));
        await transaction.CommitAsync();

        // Assert: re-read from the database (AsNoTracking skips EF's cache) to see what was really saved.
        var savedEvent = await db.Events.AsNoTracking().SingleAsync();
        Assert.Equal(2, savedEvent.TicketsSold);
        Assert.Equal(DateTimeKind.Utc, savedEvent.StartsAtUtc.Kind);
    }

    /// <summary>
    /// Cancels an order with 2 tickets: both registrations flip to Canceled, the event's
    /// TicketsSold drops back to 0, and canceling a second time does nothing.
    /// </summary>
    [Fact]
    public async Task Cancellation_marks_registrations_and_decrements_event_count()
    {
        // Arrange: a venue, an event with 2 of 3 tickets sold, and a ticket type for it.
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

        // Save in steps because each row needs the id of the one before it.
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
            UnitPrice = 10,
            ProductNameSnapshot = "General Admission"
        };

        db.OrderLines.Add(orderLine);
        await db.SaveChangesAsync();

        // One registration per ticket bought (the order line has quantity 2).
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

        // Act: cancel the order, then try again to prove a second cancel is harmless.
        var repository = new TicketBookingRepository(db);
        var canceledAtUtc = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);
        var canceledCount = await repository.CancelOrderRegistrationsAsync(order.OrderId, canceledAtUtc);
        var canceledAgain = await repository.CancelOrderRegistrationsAsync(order.OrderId, canceledAtUtc);

        // Assert: both tickets canceled once, nothing the second time, and the seats came back.
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

    /// <summary>
    /// Builds an event that starts tomorrow and lasts one hour, with the given capacity
    /// and tickets already sold. Not saved until the test adds it to the database.
    /// </summary>
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

    /// <summary>
    /// Opens an in-memory SQLite database. It lives only as long as the connection, so every
    /// test starts clean. Keep the connection open until the test ends or the data disappears.
    /// </summary>
    private static async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }
}
