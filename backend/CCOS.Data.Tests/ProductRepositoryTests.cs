using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Data.Tests;

/// <summary>
/// Tests for ProductRepository CRUD operations (the ticket AC). Covers create, update,
/// soft delete, and the visibility rules that make soft-deleted products disappear from
/// browse lists while officers can still see them.
/// </summary>
public class ProductRepositoryTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Create
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// CreateAsync saves a new product with IsActive = true, and it shows up in
    /// the browse list immediately.
    /// </summary>
    [Fact]
    public async Task Create_saves_product_and_appears_in_browse_list()
    {
        await using var connection = await CreateConnectionAsync();
        var options = BuildOptions(connection);
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new ProductRepository(db);

        // Act: create a product.
        var product = await repository.CreateAsync(
            category: "Swag",
            name: "Club T-Shirt",
            description: "Comfy cotton tee with the logo.",
            price: 25.00m,
            memberPrice: 20.00m,
            stockQuantity: 50);

        // Assert: row exists with the right values.
        var saved = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal("Swag", saved.Category);
        Assert.Equal("Club T-Shirt", saved.Name);
        Assert.Equal("Comfy cotton tee with the logo.", saved.Description);
        Assert.Equal(25.00m, saved.Price);
        Assert.Equal(20.00m, saved.MemberPrice);
        Assert.Equal(50, saved.StockQuantity);
        Assert.True(saved.IsActive);

        // The product shows up in the browse list.
        var browseList = await repository.ListActiveMerchandiseAsync();
        Assert.Single(browseList);
        Assert.Equal("Club T-Shirt", browseList[0].Name);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// UpdateAsync changes all fields and persists them.
    /// </summary>
    [Fact]
    public async Task Update_changes_all_fields()
    {
        await using var connection = await CreateConnectionAsync();
        var options = BuildOptions(connection);
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new ProductRepository(db);
        var product = await repository.CreateAsync("Swag", "Old Name", null, 10m, 8m, 5);

        // Act: update everything.
        var updated = await repository.UpdateAsync(
            product.ProductId,
            category: "Resources",
            name: "New Name",
            description: "Now with a description!",
            price: 15m,
            memberPrice: 12m,
            stockQuantity: 100,
            isActive: true);

        // Assert: the returned object has the new values.
        Assert.NotNull(updated);
        Assert.Equal("Resources", updated.Category);
        Assert.Equal("New Name", updated.Name);
        Assert.Equal("Now with a description!", updated.Description);
        Assert.Equal(15m, updated.Price);
        Assert.Equal(12m, updated.MemberPrice);
        Assert.Equal(100, updated.StockQuantity);

        // And they're persisted.
        var saved = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal("New Name", saved.Name);
    }

    /// <summary>
    /// UpdateAsync returns null for an unknown id.
    /// </summary>
    [Fact]
    public async Task Update_returns_null_for_unknown_id()
    {
        await using var connection = await CreateConnectionAsync();
        var options = BuildOptions(connection);
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new ProductRepository(db);

        // Act: try to update a product that doesn't exist.
        var result = await repository.UpdateAsync(
            productId: 9999,
            category: "Swag",
            name: "Ghost",
            description: null,
            price: 10m,
            memberPrice: 10m,
            stockQuantity: 1,
            isActive: true);

        // Assert: null means not found.
        Assert.Null(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Soft Delete
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SoftDeleteAsync sets IsActive = false but keeps the row in the database.
    /// </summary>
    [Fact]
    public async Task SoftDelete_keeps_row_with_IsActive_false()
    {
        await using var connection = await CreateConnectionAsync();
        var options = BuildOptions(connection);
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new ProductRepository(db);
        var product = await repository.CreateAsync("Swag", "Doomed Hoodie", null, 50m, 45m, 10);

        // Act: soft delete.
        var success = await repository.SoftDeleteAsync(product.ProductId);

        // Assert: row still exists with IsActive = false.
        Assert.True(success);
        var saved = await db.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync();
        Assert.False(saved.IsActive);
        Assert.Equal("Doomed Hoodie", saved.Name);
    }

    /// <summary>
    /// After soft delete, the product is gone from the browse list (ListActiveMerchandiseAsync)
    /// but still shows in the officer list (ListAllMerchandiseAsync).
    /// </summary>
    [Fact]
    public async Task SoftDelete_removes_from_browse_list_but_stays_in_officer_list()
    {
        await using var connection = await CreateConnectionAsync();
        var options = BuildOptions(connection);
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new ProductRepository(db);
        var product = await repository.CreateAsync("Swag", "Discontinued Mug", null, 15m, 12m, 3);

        // Act: soft delete.
        await repository.SoftDeleteAsync(product.ProductId);

        // Assert: gone from browse list, present in officer list.
        var browseList = await repository.ListActiveMerchandiseAsync();
        var officerList = await repository.ListAllMerchandiseAsync();

        Assert.Empty(browseList);
        Assert.Single(officerList);
        Assert.Equal("Discontinued Mug", officerList[0].Name);
        Assert.False(officerList[0].IsActive);
    }

    /// <summary>
    /// SoftDeleteAsync returns false for an unknown id.
    /// </summary>
    [Fact]
    public async Task SoftDelete_returns_false_for_unknown_id()
    {
        await using var connection = await CreateConnectionAsync();
        var options = BuildOptions(connection);
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new ProductRepository(db);

        // Act: try to delete a product that doesn't exist.
        var success = await repository.SoftDeleteAsync(9999);

        // Assert: false means not found.
        Assert.False(success);
    }

    /// <summary>
    /// SoftDeleteAsync returns false for an already-inactive product (can't delete twice).
    /// </summary>
    [Fact]
    public async Task SoftDelete_returns_false_for_already_inactive_product()
    {
        await using var connection = await CreateConnectionAsync();
        var options = BuildOptions(connection);
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new ProductRepository(db);
        var product = await repository.CreateAsync("Swag", "Double Delete Test", null, 10m, 10m, 1);

        // Act: delete twice.
        var firstDelete = await repository.SoftDeleteAsync(product.ProductId);
        var secondDelete = await repository.SoftDeleteAsync(product.ProductId);

        // Assert: first succeeds, second fails.
        Assert.True(firstDelete);
        Assert.False(secondDelete);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FindActiveMerchandiseAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// FindActiveMerchandiseAsync returns null for an unknown id.
    /// </summary>
    [Fact]
    public async Task Find_returns_null_for_unknown_id()
    {
        await using var connection = await CreateConnectionAsync();
        var options = BuildOptions(connection);
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new ProductRepository(db);

        // Act: try to find a product that doesn't exist.
        var result = await repository.FindActiveMerchandiseAsync(9999);

        // Assert: null means not found.
        Assert.Null(result);
    }

    /// <summary>
    /// FindActiveMerchandiseAsync returns null for a soft-deleted product.
    /// </summary>
    [Fact]
    public async Task Find_returns_null_for_soft_deleted_product()
    {
        await using var connection = await CreateConnectionAsync();
        var options = BuildOptions(connection);
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new ProductRepository(db);
        var product = await repository.CreateAsync("Swag", "Invisible Product", null, 10m, 10m, 1);
        await repository.SoftDeleteAsync(product.ProductId);

        // Act: try to find the deleted product.
        var result = await repository.FindActiveMerchandiseAsync(product.ProductId);

        // Assert: null because it's inactive.
        Assert.Null(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Reactivate
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ReactivateAsync brings a soft-deleted product back to the browse list.
    /// </summary>
    [Fact]
    public async Task Reactivate_restores_product_to_browse_list()
    {
        await using var connection = await CreateConnectionAsync();
        var options = BuildOptions(connection);
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new ProductRepository(db);
        var product = await repository.CreateAsync("Swag", "Zombie Product", null, 20m, 18m, 5);
        await repository.SoftDeleteAsync(product.ProductId);

        // Sanity check: it's gone from the browse list.
        Assert.Empty(await repository.ListActiveMerchandiseAsync());

        // Act: reactivate.
        var success = await repository.ReactivateAsync(product.ProductId);

        // Assert: it's back in the browse list.
        Assert.True(success);
        var browseList = await repository.ListActiveMerchandiseAsync();
        Assert.Single(browseList);
        Assert.Equal("Zombie Product", browseList[0].Name);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ordering Tests (from HEAD)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListActiveMerchandiseAsync_returns_active_merchandise_ordered_by_category_then_name()
    {
        await using var connection = await CreateConnectionAsync();
        var options = BuildOptions(connection);
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
        var options = BuildOptions(connection);
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
        var options = BuildOptions(connection);
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
        var options = BuildOptions(connection);
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
        var options = BuildOptions(connection);
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
        var options = BuildOptions(connection);
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new ProductRepository(db);
        var result = await repository.FindActiveMerchandiseAsync(999);

        Assert.Null(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens an in-memory SQLite database. Keep the connection open until the test ends.
    /// </summary>
    private static async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>
    /// Builds DbContextOptions for in-memory SQLite.
    /// </summary>
    private static DbContextOptions<AppDbContext> BuildOptions(SqliteConnection connection) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
}
