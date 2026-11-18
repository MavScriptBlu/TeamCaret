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
    /// <summary>The product to show. Set by OnGetAsync before the page draws.</summary>
    public ProductDetailViewModel Product { get; private set; } = default!;

    /// <summary>How many of this product are already in the visitor's cart (CAR-92).</summary>
    public int InCart { get; private set; }

    /// <summary>
    /// Loads one product for the page. Returns 404 when the id is missing or no active product
    /// matches, and 503 when the API can't be reached. Both show the friendly status page
    /// (Pages/Status.cshtml, CAR-60) instead of a blank browser error.
    /// </summary>
    /// <param name="id">The product id from the URL.</param>
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
        // Only handle "API is down or sent bad data" problems. A timeout counts, but a visitor
        // closing the page (cancellation) is not an error worth logging.
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

        // Copy just the fields this page shows.
        Product = new ProductDetailViewModel(
            product.ProductId,
            product.Category,
            product.Name,
            product.Description,
            product.Price,
            product.MemberPrice,
            product.StockQuantity);
        InCart = HttpContext.GetCart().QuantityOf(CartItemKind.Product, product.ProductId);
        return Page();
    }

    /// <summary>
    /// The Add to cart button (CAR-92). Checks the stock again with the API, so the cart never
    /// holds more than is on hand, then comes back to this page with a message.
    /// </summary>
    /// <param name="id">The product id from the URL.</param>
    /// <param name="quantity">How many to add, from the Qty box.</param>
    public async Task<IActionResult> OnPostAddToCartAsync(int? id, int quantity, CancellationToken cancellationToken)
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
            logger.LogError(ex, "Couldn't check product {ProductId} before adding it to the cart.", productId);
            TempData[CartMessages.ErrorKey] = CartMessages.TryAgainLater;
            return RedirectToPage("/Products/Details", new { id = productId });
        }

        if (product is null)
        {
            return NotFound();
        }

        if (quantity < 1)
        {
            TempData[CartMessages.ErrorKey] = CartMessages.PickAQuantity;
            return RedirectToPage("/Products/Details", new { id = productId });
        }

        var cart = HttpContext.GetCart();
        var result = cart.Add(CartItemKind.Product, productId, null, quantity, product.StockQuantity);
        if (result != CartAddResult.NoneAvailable)
        {
            HttpContext.SaveCart(cart);
        }

        CartMessages.Put(TempData, result, product.Name, quantity,
            cart.QuantityOf(CartItemKind.Product, productId), product.StockQuantity);
        return RedirectToPage("/Products/Details", new { id = productId });
    }

    /// <summary>
    /// The product details to display. Only exists when the product was found (a missing
    /// product returns 404 before the page draws).
    /// </summary>
    /// <param name="Id">The product id.</param>
    /// <param name="Category">The group the product belongs to, like Swag.</param>
    /// <param name="Name">The product name.</param>
    /// <param name="Description">Optional description text.</param>
    /// <param name="Price">The regular price.</param>
    /// <param name="MemberPrice">The member price, or null when it shouldn't be shown.</param>
    /// <param name="StockQuantity">How many are on hand. 0 means out of stock.</param>
    public sealed record ProductDetailViewModel(
    int Id,
    string Category,
    string Name,
    string? Description,
    decimal Price,
    decimal? MemberPrice,
    int StockQuantity);
}
