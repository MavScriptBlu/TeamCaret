using CCOS.Shared;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Officer.Products;

/// <summary>
/// Page model for "/Officer/Products/Create" page.
/// Officer-only: lock this down with [Authorize] once login is wired up.
/// </summary>
/// TODO: add [Authorize] once login is wired up
public class CreateModel(ProductsApiClient productsApi, ILogger<CreateModel> logger) : PageModel
{
    [BindProperty]
    public ProductInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new CreateProductRequest(
            Input.Category,
            Input.Name,
            Input.Description,
            Input.Price!.Value,
            Input.MemberPrice!.Value,
            Input.StockQuantity!.Value);

        try
        {
            using var response = await productsApi.CreateAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to create product. Status code: {StatusCode}", response.StatusCode);

                ModelState.AddModelError(string.Empty, "The product could not be created. Please try again");

                return Page();
            }
            
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The product API could not be reached");

            ModelState.AddModelError(string.Empty, "The product API could not be reached. Please try again later");

            return Page();
        }
        catch (TaskCanceledException ex)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The product API request timed out");

            ModelState.AddModelError(string.Empty, "The product API request timed out. Please try again later");

            return Page();
        }

        TempData["ProductCreated"] = $"{Input.Name} was added successfully";

        return RedirectToPage("/Officer/Products/Index");

    }

}
