using CCOS.Shared;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Officer.Products;

/// <summary>
/// Page model for "/Officer/Products/Create" page. Shows the blank product form, checks
/// what the officer typed, and sends the new product to CCOS.Api.
/// Officer-only: lock this down with [Authorize] once login is wired up.
/// </summary>
public class CreateModel(ProductsApiClient productsApi, ILogger<CreateModel> logger) : PageModel
{
    /// <summary>The values typed into the form. Filled in automatically when the form is posted.</summary>
    [BindProperty]
    public ProductInput Input { get; set; } = new();

    /// <summary>Shows the blank form. Nothing to load.</summary>
    public void OnGet()
    {
    }

    /// <summary>
    /// Runs when the form is submitted. Redisplays the form with messages if the input is invalid
    /// or the API call fails, and goes back to the product list when the product was created.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Stop here and redisplay the form with the validation messages (see ProductInput).
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // The Required rules on ProductInput guarantee these numbers have values, so ! and .Value are safe.
        var request = new CreateProductRequest(
            Input.Category,
            Input.Name,
            Input.Description,
            Input.Price!.Value,
            Input.MemberPrice!.Value,
            Input.StockQuantity!.Value);

        try
        {
            // Send the new product to the API.
            using var response = await productsApi.CreateAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to create product. Status code: {StatusCode}", response.StatusCode);

                ModelState.AddModelError(string.Empty, "The product could not be created. Please try again");

                return Page();
            }
        }
        // The API is down or unreachable.
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The product API could not be reached");

            ModelState.AddModelError(string.Empty, "The product API could not be reached. Please try again later");

            return Page();
        }
        // The API took too long. (A visitor closing the page is not treated as an error.)
        catch (TaskCanceledException ex)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The product API request timed out");

            ModelState.AddModelError(string.Empty, "The product API request timed out. Please try again later");

            return Page();
        }

        // TempData lives for one more page load, so the list page can show this "success" message.
        TempData["ProductCreated"] = $"{Input.Name} was added successfully";

        return RedirectToPage("/Officer/Products/Index");
    }
}
