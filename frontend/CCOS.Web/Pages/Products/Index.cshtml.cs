using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Products;

/// <summary>
/// Product browse page (/Products): active club merchandise from CCOS.Api, with name,
/// description, category, regular price, member price and stock. Out-of-stock products
/// are shown greyed out. Inactive products never reach this page (CAR-1 design).
/// </summary>
public class IndexModel(ProductsApiClient productsApi, ILogger<IndexModel> logger) : PageModel
{
    /// <summary>
    /// Category filter from the URL (/Products?category=Swag). Empty means all categories.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    /// <summary>
    /// When true (/Products?inStockOnly=true), hide out-of-stock products.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public bool InStockOnly { get; set; }

    /// <summary>
    /// Products to show after filtering.
    /// </summary>
    public IReadOnlyList<ProductDto> Products { get; private set; } = [];

    /// <summary>
    /// Every category that has active products, for the Category filter.
    /// </summary>
    public IReadOnlyList<string> Categories { get; private set; } = [];

    /// <summary>
    /// Number of active products before filtering. Zero means nothing is for sale yet.
    /// </summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// True when the API couldn't be reached, so the page shows an error message instead.
    /// </summary>
    public bool LoadFailed { get; private set; }

    /// <summary>
    /// True when a filter is applied.
    /// </summary>
    public bool IsFiltered => !string.IsNullOrEmpty(Category) || InStockOnly;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ProductDto> all;
        try
        {
            all = await productsApi.ListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't load products from CCOS.Api.");
            LoadFailed = true;
            return;
        }

        TotalCount = all.Count;
        Categories = all.Select(product => product.Category).Distinct().Order().ToList();
        Products = all
            .Where(product => string.IsNullOrEmpty(Category) || product.Category == Category)
            .Where(product => !InStockOnly || product.StockQuantity > 0)
            .ToList();
    }
}
