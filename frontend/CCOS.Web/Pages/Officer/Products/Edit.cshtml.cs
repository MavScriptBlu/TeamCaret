using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CCOS.Web.Services;
using System.Text.Json;
using System.Net;
using CCOS.Shared;

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
                await AddApiValidationErrorsAsync(response, cancellationToken);
                return Page();
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Failed to update product {ProductId}. Status code: {StatusCode}",
                    id,
                    response.StatusCode);

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


    /// <summary>
    /// Reads validation errors from an HTTP response and adds them to ModelState.
    /// </summary>
    private async Task AddApiValidationErrorsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ValidationProblemDetails? problem;

        try
        {
            problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
        }
        catch (JsonException)
        {
            problem = null;
        }

        if (problem is null || problem.Errors.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "The product could not be saved. Please check the values and try again.");
            return;
        }

        // Add each validation error to ModelState, prefixing keys with "Input." if not already present
        foreach (var (key, messages) in problem.Errors)
        {
            var modelStateKey = key.StartsWith("Input.", StringComparison.OrdinalIgnoreCase)
                ? key
                : $"Input.{key}";

            foreach (var message in messages)
            {
                ModelState.AddModelError(modelStateKey, message);
            }
        }
    }

    private IActionResult ShowProductNotFound()
    {
        ProductNotFound = true;
        Response.StatusCode = StatusCodes.Status404NotFound;
        return Page();
    }
}
