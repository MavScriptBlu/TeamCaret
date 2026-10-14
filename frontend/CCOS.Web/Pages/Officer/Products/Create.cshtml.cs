using CCOS.Shared;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace CCOS.Web.Pages.Officer.Products;

/// <summary>
/// Page model for "/Officer/Products/Create" page.
/// Officer-only: lock this down with [Authorize] once login is wired up.
/// </summary>
/// TODO: add [Authorize] once login is wired up
public class CreateModel(ProductsApiClient productsApi, ILogger<CreateModel> logger) : PageModel
{
    [BindProperty]
    public ProductCreateInput Input { get; set; } = new();

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


    /// <summary>
    /// Input and validation for creating a new product
    /// implements IValidatableObject to validate that the regular price is not less than the member price
    /// </summary>
    public sealed class ProductCreateInput : IValidatableObject
    {
        [Required]
        [StringLength(30)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required]
        [Range(typeof(decimal), "0", "10000.00", ErrorMessage = "The regular price must be between 0 and 10000.00")]
        [Display(Name = "Regular Price")]
        public decimal? Price { get; set; }

        [Required]
        [Range(typeof(decimal), "0", "10000.00", ErrorMessage = "The member price must be between 0 and 10000.00")]
        public decimal? MemberPrice { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "The stock quantity must be 0 or greater")]
        public int? StockQuantity { get; set; }

        // New products are active by default, not shown on the form
        public bool IsActive { get; set; } = true;


        // Implement IValidatableObject to validate that the regular price is not less than the member price
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Price is decimal regularPrice && MemberPrice is decimal memberPrice && memberPrice > regularPrice)
            {
                yield return new ValidationResult("the member price cannot be greater than the regular price", new[] { nameof(MemberPrice) });
            }
        }
    }

}
