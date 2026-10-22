using CCOS.Data;
using CCOS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Api;

/// <summary>
/// Local development database: a SQLite file used when no ConnectionStrings:CCOS is set,
/// so the app runs without a database server. A real PostgreSQL database uses the
/// EF Core migrations instead.
/// </summary>
public static class DevelopmentData
{
    /// <summary>
    /// Makes sure the database has its tables, then adds sample products the first time.
    /// SQLite builds its tables directly. PostgreSQL applies the EF Core migrations.
    /// The samples cover regular and member pricing, out of stock and an inactive product
    /// (see docs/Product_Migration_Scope.md).
    /// </summary>
    public static async Task EnsureCreatedAndSeededAsync(IServiceProvider services, bool useLocalDatabase)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (useLocalDatabase)
        {
            await db.Database.EnsureCreatedAsync();
        }
        else
        {
            // Creates or updates the tables, same as `task migrate`. Skips migrations already applied.
            await db.Database.MigrateAsync();
        }

        // IgnoreQueryFilters so an inactive-only table still counts as already seeded.
        if (await db.Products.IgnoreQueryFilters().AnyAsync())
        {
            return;
        }

        // The hoodie goes first so it gets ProductId 1, matching the sample on Products/Details.
        db.Products.AddRange(
            new Product
            {
                Category = "Swag",
                Name = "Club Hoodie",
                Description = "A comfortable Cyber Cougars hoodie for showing your club pride.",
                Price = 35.00m,
                MemberPrice = 28.00m,
                StockQuantity = 42,
            },
            new Product
            {
                Category = "Swag",
                Name = "Club T-Shirt",
                Description = "Cyber Cougars logo tee.",
                Price = 15.00m,
                MemberPrice = 12.00m,
                StockQuantity = 30,
            },
            new Product
            {
                Category = "Swag",
                Name = "Sticker Pack",
                Description = "A set of club stickers.",
                Price = 5.00m,
                MemberPrice = 3.00m,
                StockQuantity = 0,
            },
            new Product
            {
                Category = "Resources",
                Name = "Study Guide",
                Description = "Printed study guide for club workshops.",
                Price = 8.00m,
                MemberPrice = 5.00m,
                StockQuantity = 12,
            },
            new Product
            {
                Category = "Swag",
                Name = "Old Mug",
                Description = "Retired product, kept for order history.",
                Price = 10.00m,
                MemberPrice = 8.00m,
                StockQuantity = 5,
                IsActive = false,
            });

        await db.SaveChangesAsync();
    }
}
