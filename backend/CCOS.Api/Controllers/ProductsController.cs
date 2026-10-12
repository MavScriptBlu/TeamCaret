using CCOS.Data.Entities;
using CCOS.Repositories;
using CCOS.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Api.Controllers;

/// <summary>
/// Product endpoints from docs/CAR-1_Product_CRUD_Design.md. Customers only get active
/// merchandise; the officer list asks for inactive products too.
/// </summary>
[ApiController]
[Route("api/products")]
public class ProductsController(ProductRepository products) : ControllerBase
{
    /// <summary>
    /// GET /api/products: active merchandise for browsing.
    /// GET /api/products?includeInactive=true: all merchandise for the officer product list,
    /// with full member prices.
    /// TODO: restrict includeInactive to officers once login exists (CAR-1: Officer only).
    /// </summary>
    [HttpGet]
    public async Task<IReadOnlyList<ProductDto>> List(
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
    {
        if (includeInactive)
        {
            var all = await products.ListAllMerchandiseAsync(cancellationToken);
            return all.Select(product => ToDto(product, includeMemberPrice: true)).ToList();
        }

        var items = await products.ListActiveMerchandiseAsync(cancellationToken);
        return items.Select(product => ToDto(product, CanSeeMemberPrice())).ToList();
    }

    /// <summary>
    /// GET /api/products/{id}: one active product, or 404.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> Get(int id, CancellationToken cancellationToken)
    {
        var product = await products.FindActiveMerchandiseAsync(id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        return ToDto(product, CanSeeMemberPrice());
    }

    /// <summary>
    /// Per the CAR-1 design, only a logged-in customer with a current Member record gets
    /// member pricing. There's no login yet, so member prices are shared with everyone
    /// for now. TODO: check the caller's Member record once ASP.NET Core Identity is in.
    /// </summary>
    private static bool CanSeeMemberPrice() => true;

    private static ProductDto ToDto(Product product, bool includeMemberPrice) => new(
        product.ProductId,
        product.Category,
        product.Name,
        product.Description,
        product.Price,
        includeMemberPrice ? product.MemberPrice : null,
        product.StockQuantity,
        product.IsActive);
}
