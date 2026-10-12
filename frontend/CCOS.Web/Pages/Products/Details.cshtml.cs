using System.Text.Json;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Products;

/// <summary>
/// Product Details page (/Products/Details/{id}). Loads the product from CCOS.Api
/// (GET /api/products/{id}); inactive or unknown products return 404.
/// </summary>
public class DetailsModel(ProductsApiClient productsApi, ILogger<DetailsModel> logger) : PageModel
{
    public ProductDetailViewModel Product { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken cancellationToken)
    {
        if (id is not int productId)
        {
            return NotFound();
        }

        CCOS.Shared.ProductDto? product;
        try
        {
            product = await productsApi.GetAsync(productId, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't load product {ProductId} from CCOS.Api.", productId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (product is null)
        {
            return NotFound();
        }

        Product = new ProductDetailViewModel(
            product.ProductId,
            product.Category,
            product.Name,
            product.Description,
            product.Price,
            product.MemberPrice,
            product.StockQuantity);
        return Page();
    }

    /// <summary>
    /// The product details to display. Null when the product is not found or the id is null.
    /// </summary>
    /// <param name="Id"></param>
    /// <param name="Category"></param>
    /// <param name="Name"></param>
    /// <param name="Description"></param>
    /// <param name="Price"></param>
    /// <param name="MemberPrice"></param>
    /// <param name="StockQuantity"></param>
    public sealed record ProductDetailViewModel(
    int Id,
    string Category,
    string Name,
    string? Description,
    decimal Price,
    decimal? MemberPrice,
    int StockQuantity);
}
