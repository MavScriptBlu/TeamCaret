using CCOS.Data.Entities;
using CCOS.Repositories;
using CCOS.Shared;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Api.Controllers;

/// <summary>
/// Product endpoints from docs/Jira-Archive/CAR-1_Product_CRUD_Design.md. Customers only get active
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
        // Officer view: everything, including deactivated products, with member prices.
        if (includeInactive)
        {
            var all = await products.ListAllMerchandiseAsync(cancellationToken);
            return all.Select(product => ToDto(product, includeMemberPrice: true)).ToList();
        }

        // Customer view: active products only.
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

        // Missing, inactive, or a ticket type: all look the same to the caller (404).
        if (product is null)
        {
            return NotFound();
        }

        return ToDto(product, CanSeeMemberPrice());
    }

    /// <summary>
    /// POST /api/products: CREATE a new product.
    /// TODO: restrict to officers once login exists (CAR-1: Officer only).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(
        [FromBody] CreateProductRequest input,
        CancellationToken cancellationToken)
    {
        var product = await products.CreateAsync(
            input.Category,
            input.Name,
            input.Description,
            input.Price,
            input.MemberPrice,
            input.StockQuantity,
            cancellationToken);

        return CreatedAtAction(
            nameof(Get),
            new { id = product.ProductId },
            ToDto(product, includeMemberPrice: true));
    }

    /// <summary>
    /// PUT /api/products/{id}: UPDATE an existing product including IsActive.
    /// TODO: restrict to officers once login exists (CAR-1: Officer only).
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductDto>> Update(
        int id,
        [FromBody] UpdateProductRequest input,
        CancellationToken cancellationToken)
    {
        var product = await products.UpdateAsync(
            id,
            input.Category,
            input.Name,
            input.Description,
            input.Price,
            input.MemberPrice,
            input.StockQuantity,
            input.IsActive,
            cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        return ToDto(product, includeMemberPrice: true);
    }

    /// <summary>
    /// PATCH /api/products/{id}/reactivate: reactivate a soft-deleted product.
    /// TODO: restrict to officers once login exists (CAR-1: Officer only).
    /// </summary>
    [HttpPatch("{id:int}/reactivate")]
    public async Task<IActionResult> Reactivate(int id, CancellationToken cancellationToken)
    {
        var success = await products.ReactivateAsync(id, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    /// <summary>
    /// DELETE /api/products/{id}: SOFT-DELETE a product (IsActive = false).
    /// Rows are never removed per acceptance criteria.
    /// TODO: restrict to officers once login exists (CAR-1: Officer only).
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var success = await products.SoftDeleteAsync(id, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    /// <summary>
    /// Per the CAR-1 design, only a logged-in customer with a current Member record gets
    /// member pricing. There's no login yet, so member prices are shared with everyone
    /// for now. TODO: check the caller's Member record once ASP.NET Core Identity is in.
    /// </summary>
    private static bool CanSeeMemberPrice() => true;

    /// <summary>
    /// Turns a database Product into the shape sent over the wire. The member price is left
    /// out (null) when includeMemberPrice is false.
    /// </summary>
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
