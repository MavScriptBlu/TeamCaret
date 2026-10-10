using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Products;

/// <summary>
/// Stub for the Product Details page (/Products/Details/{id}). Content is added in a later story.
/// </summary>
public class DetailsModel : PageModel
{
    // Temporary sample data, following the sample-data approach on Events/Index.
    private static readonly ProductDetailViewModel SampleProduct = new(
        Id: 1,
        Category: "Swag",
        Name: "Club Hoodie",
        Description: "A comfortable Cyber Cougars hoodie for showing your club pride.",
        Price: 35.00m,
        MemberPrice: 28.00m,
        StockQuantity: 42);

    public ProductDetailViewModel Product { get; private set; } = default!;

    public IActionResult OnGet(int? id)
    {
        if (id != SampleProduct.Id)
        {
            return NotFound();
        }

        Product = SampleProduct;
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
    string Description,
    decimal Price,
    decimal? MemberPrice,
    int StockQuantity);
}
