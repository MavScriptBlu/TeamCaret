using System.Net;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Extentions;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Officer.Products;

/// <summary>
/// Page model for (/Officer/Products/Edit/{id})
/// Officer-only: lock this down with [Authorize] once login is wired up.
/// </summary>
/// TODO: add [Authorize] once login is wired up
public class EditModel(ProductsApiClient productsApi, ILogger<EditModel> logger) : PageModel
{
    [BindProperty]
    public ProductInput Input { get; set; } = new();

    public bool ProductNotFound { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            var products = await productsApi.GetForEditAsync(id, cancellationToken);

            if (products is null)
            {
                return ShowProductNotFound();
            }

            Input = new ProductInput
            {
                Category = products.Category,
                Name = products.Name,
                Description = products.Description,
                Price = products.Price,
                MemberPrice = products.MemberPrice,
                StockQuantity = products.StockQuantity,
                IsActive = products.IsActive
            };

            return Page();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Product API could not be reached for product {ProductId}", id);
            ModelState.AddModelError(string.Empty, "The product API could not be reached. Please try again later");
            return Page();
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Product API returned invalid JSON for product {ProductId}", id);
            ModelState.AddModelError(string.Empty, "The product data could not be loaded. Please try again later");
            return Page();
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "The product API request timed out while loading product {ProductId}", id);
            ModelState.AddModelError(string.Empty, "The product API request timed out. Please try again later");
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new UpdateProductRequest(
            Input.Category,
            Input.Name,
            Input.Description,
            Input.Price!.Value,
            Input.MemberPrice!.Value,
            Input.StockQuantity!.Value,
            Input.IsActive);

        try
        {
            using var response = await productsApi.UpdateAsync(id, request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return ShowProductNotFound();
            }

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
            {
                await ModelState.AddApiValidationErrorsAsync(
                    response,
                    nameof(Input),
                    "The product could not be saved. Please check the values and try again.",
                    cancellationToken);

                return Page();
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to update product {ProductId}. Status code: {StatusCode}", id, response.StatusCode);

                ModelState.AddModelError(string.Empty, "The product could not be saved. Please try again.");

                return Page();
            }

            TempData["ProductSaved"] = "Saved";
            return RedirectToPage("/Officer/Products/Index");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The product API could not be reached while saving product {ProductId}", id);

            ModelState.AddModelError(string.Empty, "The product API could not be reached. Please try again later.");

            return Page();
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The product API request timed out while saving product {ProductId}", id);

            ModelState.AddModelError(string.Empty, "The product API request timed out. Please try again later.");

            return Page();
        }
    }

    private IActionResult ShowProductNotFound()
    {
        ProductNotFound = true;
        Response.StatusCode = StatusCodes.Status404NotFound;
        return Page();
    }
}
