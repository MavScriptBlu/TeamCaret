using CCOS.Data;
using CCOS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Repositories;

/// <summary>
/// Reads club merchandise (swag and resources).
/// Inactive products are left out by the IsActive query filter on Product (CAR-24),
/// except in ListAllMerchandiseAsync, which officer screens use.
/// </summary>
public sealed class ProductRepository(AppDbContext db)
{
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
}
