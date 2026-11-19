using CCOS.Data.Entities;
using CCOS.Repositories;
using CCOS.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Api.Controllers;

/// <summary>
/// Product endpoints from docs/Jira-Archive/CAR-1_Product_CRUD_Design.md. Customers only get active
/// merchandise; the officer list asks for inactive products too.
/// Access (CAR-69): browsing is open to everyone, member prices only go to current members, and
/// the officer list plus create, update, reactivate and remove need the Officer policy
/// (401 when not signed in, 403 when signed in but not an officer).
/// </summary>
[ApiController]
[Route("api/products")]
public class ProductsController(ProductRepository products, IAuthorizationService authorization) : ControllerBase
{
    /// <summary>
    /// GET /api/products: active merchandise for browsing.
    /// GET /api/products?includeInactive=true: all merchandise for the officer product list,
    /// with full member prices. Officers only (401 / 403 otherwise).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> List(
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
    {
        // Officer view: everything, including deactivated products, with member prices.
        if (includeInactive)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Challenge();
            }

            if (!(await authorization.AuthorizeAsync(User, ClubPolicies.Officer)).Succeeded)
            {
                return Forbid();
            }

            var all = await products.ListAllMerchandiseAsync(cancellationToken);
            return Ok(all.Select(product => ToDto(product, includeMemberPrice: true)).ToList());
        }

        // Customer view: active products only.
        var items = await products.ListActiveMerchandiseAsync(cancellationToken);
        var includeMemberPrice = await CanSeeMemberPriceAsync();
        return Ok(items.Select(product => ToDto(product, includeMemberPrice)).ToList());
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

        return ToDto(product, await CanSeeMemberPriceAsync());
    }

    /// <summary>
    /// POST /api/products: CREATE a new product. Officers only.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = ClubPolicies.Officer)]
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
    /// PUT /api/products/{id}: UPDATE an existing product including IsActive. Officers only.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = ClubPolicies.Officer)]
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
    /// PATCH /api/products/{id}/reactivate: reactivate a soft-deleted product. Officers only.
    /// </summary>
    [HttpPatch("{id:int}/reactivate")]
    [Authorize(Policy = ClubPolicies.Officer)]
    public async Task<IActionResult> Reactivate(int id, CancellationToken cancellationToken)
    {
        var success = await products.ReactivateAsync(id, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    /// <summary>
    /// DELETE /api/products/{id}: SOFT-DELETE a product (IsActive = false).
    /// Rows are never removed per acceptance criteria. Officers only.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = ClubPolicies.Officer)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var success = await products.SoftDeleteAsync(id, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    /// <summary>
    /// Per the CAR-1 design, only a signed-in customer who is a current member (dues paid
    /// through today) gets member pricing: the Member policy, checked against the database on
    /// every request (CAR-69). Guests, expired members and visitors who aren't signed in get null.
    /// </summary>
    private async Task<bool> CanSeeMemberPriceAsync() =>
        User.Identity?.IsAuthenticated == true
        && (await authorization.AuthorizeAsync(User, ClubPolicies.Member)).Succeeded;

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
