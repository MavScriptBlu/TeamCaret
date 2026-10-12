using CCOS.Data;
using CCOS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Repositories;

/// <summary>
/// Reads club merchandise (swag and resources) for the customer-facing pages.
/// Inactive products are left out by the IsActive query filter on Product (CAR-24),
/// so callers only ever get products that are for sale.
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
    /// One active merchandise product, or null when there's no active product with that id.
    /// </summary>
    public Task<Product?> FindActiveMerchandiseAsync(int productId, CancellationToken cancellationToken = default)
    {
        return Merchandise().FirstOrDefaultAsync(product => product.ProductId == productId, cancellationToken);
    }

    private IQueryable<Product> Merchandise() =>
        db.Products.AsNoTracking().Where(product => !(product is TicketType));
}
