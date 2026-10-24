using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Data.Tests;

public class ProductRepositoryTests
{
    private static async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    [Fact]
    public async Task ListActiveMerchandiseAsync_returns_active_merchandise_ordered_by_category_then_name()
    {
        await using var connection = await CreateConnectionAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var activeProduct1 = new Product { Name = "Z Shirt", Category = "Apparel", IsActive = true, Price = 10, MemberPrice = 10 };
        var activeProduct2 = new Product { Name = "A Shirt", Category = "Apparel", IsActive = true, Price = 10, MemberPrice = 10 };
        var activeProduct3 = new Product { Name = "Mug", Category = "Swag", IsActive = true, Price = 10, MemberPrice = 10 };
        var inactiveProduct = new Product { Name = "Old Shirt", Category = "Apparel", IsActive = false, Price = 10, MemberPrice = 10 };
        var venue = new Venue { Name = "Test venue", Capacity = 10 };
        var @event = new Event
        {
            Venue = venue,
            Name = "Test event",
            StartsAtUtc = DateTime.UtcNow.AddDays(1),
            EndsAtUtc = DateTime.UtcNow.AddDays(1).AddHours(1),
            TicketCapacity = 10
        };
        var ticketType = new TicketType { Name = "General", Category = "Tickets", IsActive = true, Price = 10, MemberPrice = 10, Event = @event };

        db.Products.AddRange(activeProduct1, activeProduct2, activeProduct3, inactiveProduct, ticketType);
        await db.SaveChangesAsync();

        var repository = new ProductRepository(db);
        var result = await repository.ListActiveMerchandiseAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal("A Shirt", result[0].Name);
        Assert.Equal("Z Shirt", result[1].Name);
        Assert.Equal("Mug", result[2].Name);
    }

    [Fact]
    public async Task ListAllMerchandiseAsync_returns_all_merchandise_ordered_by_category_then_name()
    {
        await using var connection = await CreateConnectionAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var activeProduct1 = new Product { Name = "Z Shirt", Category = "Apparel", IsActive = true, Price = 10, MemberPrice = 10 };
        var activeProduct2 = new Product { Name = "A Shirt", Category = "Apparel", IsActive = true, Price = 10, MemberPrice = 10 };
        var activeProduct3 = new Product { Name = "Mug", Category = "Swag", IsActive = true, Price = 10, MemberPrice = 10 };
        var inactiveProduct = new Product { Name = "Old Shirt", Category = "Apparel", IsActive = false, Price = 10, MemberPrice = 10 };
        var venue = new Venue { Name = "Test venue", Capacity = 10 };
        var @event = new Event
        {
            Venue = venue,
            Name = "Test event",
            StartsAtUtc = DateTime.UtcNow.AddDays(1),
            EndsAtUtc = DateTime.UtcNow.AddDays(1).AddHours(1),
            TicketCapacity = 10
        };
        var ticketType = new TicketType { Name = "General", Category = "Tickets", IsActive = true, Price = 10, MemberPrice = 10, Event = @event };

        db.Products.AddRange(activeProduct1, activeProduct2, activeProduct3, inactiveProduct, ticketType);
        await db.SaveChangesAsync();

        var repository = new ProductRepository(db);
        var result = await repository.ListAllMerchandiseAsync();

        Assert.Equal(4, result.Count);
        Assert.Equal("A Shirt", result[0].Name);
        Assert.Equal("Old Shirt", result[1].Name);
        Assert.Equal("Z Shirt", result[2].Name);
        Assert.Equal("Mug", result[3].Name);
    }

    [Fact]
    public async Task FindActiveMerchandiseAsync_returns_active_merchandise()
    {
        await using var connection = await CreateConnectionAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var activeProduct = new Product { Name = "Shirt", Category = "Apparel", IsActive = true, Price = 10, MemberPrice = 10 };
        db.Products.Add(activeProduct);
        await db.SaveChangesAsync();

        var repository = new ProductRepository(db);
        var result = await repository.FindActiveMerchandiseAsync(activeProduct.ProductId);

        Assert.NotNull(result);
        Assert.Equal(activeProduct.ProductId, result.ProductId);
    }

    [Fact]
    public async Task FindActiveMerchandiseAsync_returns_null_for_inactive_merchandise()
    {
        await using var connection = await CreateConnectionAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var inactiveProduct = new Product { Name = "Shirt", Category = "Apparel", IsActive = false, Price = 10, MemberPrice = 10 };
        db.Products.Add(inactiveProduct);
        await db.SaveChangesAsync();

        var repository = new ProductRepository(db);
        var result = await repository.FindActiveMerchandiseAsync(inactiveProduct.ProductId);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindActiveMerchandiseAsync_returns_null_for_ticket_types()
    {
        await using var connection = await CreateConnectionAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var venue = new Venue { Name = "Test venue", Capacity = 10 };
        var @event = new Event
        {
            Venue = venue,
            Name = "Test event",
            StartsAtUtc = DateTime.UtcNow.AddDays(1),
            EndsAtUtc = DateTime.UtcNow.AddDays(1).AddHours(1),
            TicketCapacity = 10
        };
        var ticketType = new TicketType { Name = "General", Category = "Tickets", IsActive = true, Price = 10, MemberPrice = 10, Event = @event };
        db.Products.Add(ticketType);
        await db.SaveChangesAsync();

        var repository = new ProductRepository(db);
        var result = await repository.FindActiveMerchandiseAsync(ticketType.ProductId);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindActiveMerchandiseAsync_returns_null_when_not_found()
    {
        await using var connection = await CreateConnectionAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new ProductRepository(db);
        var result = await repository.FindActiveMerchandiseAsync(999);

        Assert.Null(result);
    }
}
