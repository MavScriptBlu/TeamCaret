using System.Net;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Extensions;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Officer.Products;

/// <summary>
/// Page model for (/Officer/Products/Edit/{id}). Loads the product into the form, then sends
/// the changes to CCOS.Api when the officer saves.
/// Officer-only: lock this down with [Authorize] once login is wired up.
/// </summary>
public class EditModel(ProductsApiClient productsApi, ILogger<EditModel> logger) : PageModel
{
    /// <summary>The product id from the URL route. Exposed so the view can display it.</summary>
    public int? Id { get; private set; }

    /// <summary>The values in the form. Starts as the saved product, then holds what the officer typed.</summary>
    [BindProperty]
    public ProductInput Input { get; set; } = new();

    /// <summary>True when the id in the URL doesn't match a product, so the page shows "not found".</summary>
    public bool ProductNotFound { get; private set; }

    /// <summary>
    /// Loads the product and fills the form with its saved values. Shows "not found" (404)
    /// when there's no such product, and an error message when the API fails.
    /// </summary>
    /// <param name="id">The product id from the URL.</param>
    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        Id = id;

        try
        {
            var product = await productsApi.GetForEditAsync(id, cancellationToken);

            if (product is null)
            {
                return ShowProductNotFound();
            }

            // Copy the saved values into the form fields.
            Input = new ProductInput
            {
                Category = product.Category,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                MemberPrice = product.MemberPrice,
                StockQuantity = product.StockQuantity,
                IsActive = product.IsActive
            };

            return Page();
        }
        // Each catch below shows a friendly message on the page instead of crashing.
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

    /// <summary>
    /// Runs when the form is saved. Sends the changes to the API, shows any errors on the form,
    /// and goes back to the product list when the save works.
    /// </summary>
    /// <param name="id">The product id from the URL.</param>
    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        Id = id;

        // Stop here and redisplay the form with the validation messages (see ProductInput).
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // The Required rules on ProductInput guarantee these numbers have values, so ! and .Value are safe.
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

            // The product was deleted or never existed.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return ShowProductNotFound();
            }

            // The API rejected the values: show its messages next to the matching form fields.
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
            {
                await ModelState.AddApiValidationErrorsAsync(
                    response,
                    nameof(Input),
                    "The product could not be saved. Please check the values and try again.",
                    cancellationToken);

                return Page();
            }

            // Any other failure: log it and show a general message.
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to update product {ProductId}. Status code: {StatusCode}", id, response.StatusCode);

                ModelState.AddModelError(string.Empty, "The product could not be saved. Please try again.");

                return Page();
            }

            // TempData lives for one more page load, so the list page can show this "saved" message.
            TempData["ProductSaved"] = "Saved";
            return RedirectToPage("/Officer/Products/Index");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The product API could not be reached while saving product {ProductId}", id);

            ModelState.AddModelError(string.Empty, "The product API could not be reached. Please try again later.");

            return Page();
        }
        // The API took too long. (A visitor closing the page is not treated as an error.)
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The product API request timed out while saving product {ProductId}", id);

            ModelState.AddModelError(string.Empty, "The product API request timed out. Please try again later.");

            return Page();
        }
    }

    /// <summary>
    /// Marks the page as "product not found" and returns it with a 404 status.
    /// </summary>
    private IActionResult ShowProductNotFound()
    {
        ProductNotFound = true;
        Response.StatusCode = StatusCodes.Status404NotFound;
        return Page();
    }
}
