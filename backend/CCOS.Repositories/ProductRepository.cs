using CCOS.Data;
using CCOS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Repositories;

/// <summary>
/// CRUD operations for club merchandise (swag and resources).
/// Inactive products are left out by the IsActive query filter on Product (CAR-24),
/// except in ListAllMerchandiseAsync, which officer screens use.
/// Delete is always soft-delete (IsActive = false); rows are never removed.
/// </summary>
public sealed class ProductRepository(AppDbContext db)
{
    // ─────────────────────────────────────────────────────────────────────────
    // Create
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// CREATEs a new merchandise product.
    /// </summary>
    public async Task<Product> CreateAsync(
        string category,
        string name,
        string? description,
        decimal price,
        decimal memberPrice,
        int stockQuantity,
        CancellationToken cancellationToken = default)
    {
        var product = new Product
        {
            Category = category,
            Name = name,
            Description = description,
            Price = price,
            MemberPrice = memberPrice,
            StockQuantity = stockQuantity,
            IsActive = true
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return product;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Read
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Active merchandise, by category then name. Ticket types are Products too
    /// (TicketType), but they're sold from event pages, so they're left out here.
    /// </summary>
    public async Task<IReadOnlyList<Product>> ListActiveMerchandiseAsync(CancellationToken cancellationToken = default)
    {
        return await Merchandise()
            .OrderBy(product => product.Category)
            .ThenBy(product => product.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// All merchandise, including inactive products, by category then name. For officer
    /// screens only (CAR-1: officers can see deactivated products).
    /// </summary>
    public async Task<IReadOnlyList<Product>> ListAllMerchandiseAsync(CancellationToken cancellationToken = default)
    {
        return await db.Products.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(product => !(product is TicketType))
            .OrderBy(product => product.Category)
            .ThenBy(product => product.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// One active merchandise product, or null when there's no active product with that id.
    /// </summary>
    public Task<Product?> FindActiveMerchandiseAsync(int productId, CancellationToken cancellationToken = default)
    {
        return Merchandise().FirstOrDefaultAsync(product => product.ProductId == productId, cancellationToken);
    }

    private IQueryable<Product> Merchandise() =>
        db.Products.AsNoTracking().Where(product => !(product is TicketType));

    // ─────────────────────────────────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// UPDATEs an existing merchandise product including IsActive. Returns the updated product,
    /// or null if not found. IgnoreQueryFilters so officers can update inactive products.
    /// </summary>
    public async Task<Product?> UpdateAsync(
        int productId,
        string category,
        string name,
        string? description,
        decimal price,
        decimal memberPrice,
        int stockQuantity,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var product = await db.Products
            .IgnoreQueryFilters()
            .Where(p => !(p is TicketType))
            .FirstOrDefaultAsync(p => p.ProductId == productId, cancellationToken);

        if (product is null)
        {
            return null;
        }

        product.Category = category;
        product.Name = name;
        product.Description = description;
        product.Price = price;
        product.MemberPrice = memberPrice;
        product.StockQuantity = stockQuantity;
        product.IsActive = isActive;

        await db.SaveChangesAsync(cancellationToken);
        return product;
    }

    /// <summary>
    /// REACTIVATEs a soft-deleted product. Returns true if found and reactivated, false if not found.
    /// </summary>
    public async Task<bool> ReactivateAsync(int productId, CancellationToken cancellationToken = default)
    {
        var product = await db.Products
            .IgnoreQueryFilters()
            .Where(p => !(p is TicketType))
            .FirstOrDefaultAsync(p => p.ProductId == productId, cancellationToken);

        if (product is null)
        {
            return false;
        }

        product.IsActive = true;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Delete (soft-delete only: IsActive = false)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SOFT-DELETEs a product (sets IsActive to false). Rows are never removed.
    /// Returns true if found and deactivated, false if not found or already inactive.
    /// </summary>
    public async Task<bool> SoftDeleteAsync(int productId, CancellationToken cancellationToken = default)
    {
        var product = await db.Products
            .Where(p => !(p is TicketType))
            .FirstOrDefaultAsync(p => p.ProductId == productId, cancellationToken);

        if (product is null)
        {
            return false;
        }

        product.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
