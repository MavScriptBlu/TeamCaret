using System.Net;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Officer.Products;

/// <summary>
/// Officer Dashboard product list (/Officer/Products). Shows every product from CCOS.Api,
/// including deactivated ones, matching Wireframe-Officer-Edit and Wireframe-SoftDelete-IsActive.
/// Delete and Quick Edit are placeholders; nothing is saved yet.
/// Officer-only: lock this down with [Authorize] once login is wired up.
/// </summary>
public class IndexModel(ProductsApiClient productsApi, ILogger<IndexModel> logger) : PageModel
{
    /// <summary>
    /// Filter from the URL: "active", "inactive", or empty for all (CAR-1 design).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Show { get; set; }

    /// <summary>
    /// Product row to expand after returning from an action.
    /// The view uses this value to reopen the affected row.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int? ExpandedId { get; set; }

    /// <summary>
    /// Products to show after filtering.
    /// </summary>
    public IReadOnlyList<ProductDto> Products { get; private set; } = [];

    /// <summary>
    /// Number of products before filtering. Zero means there are no products yet.
    /// </summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// True when the API couldn't be reached, so the page shows an error message instead.
    /// </summary>
    public bool LoadFailed { get; private set; }

    /// <summary>
    /// Loads every product (active and inactive), then applies the Show filter from the URL.
    /// If the API can't be reached, LoadFailed is set so the page shows an error message.
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ProductDto> all;
        try
        {
            all = await productsApi.ListAllAsync(cancellationToken);
        }
        // Only handle "API is down or sent bad data" problems. A timeout counts, but a visitor
        // closing the page (cancellation) is not an error worth logging.
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't load products from CCOS.Api.");
            LoadFailed = true;
            return;
        }

        // The count is taken before filtering so the page can tell "no products yet"
        // apart from "no products match this filter".
        TotalCount = all.Count;
        Products = Show switch
        {
            "active" => all.Where(product => product.IsActive).ToList(),
            "inactive" => all.Where(product => !product.IsActive).ToList(),
            _ => all,
        };
    }


    /// <summary>
    ///  handle the "Delete" button click for a product. This method attempts to deactivate the product via the API.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteAsync(int id, string? show, CancellationToken cancellationToken)
    {
        // Validate the product ID. If it's not valid, set an error message and redirect back to the list.
        if (id <= 0)
        {
            TempData["ProductDeleteError"] = "The product could not be identified. Please refresh the list and try again.";
            return RedirectToList(NormalizeFilter(show), id);
        }

        try
        {
            using var response = await productsApi.DeleteAsync(id, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogWarning("Could not deactivate product {ProductId}; it was not found or is already deactivated.", id);

                TempData["ProductDeleteError"] = "That product could not be found or is already deactivated. Refresh the list and try again.";

                // An already-inactive product won't appear in the Active filter.
                var notFoundFilter = show == "active" ? "inactive" : NormalizeFilter(show);
                return RedirectToList(notFoundFilter, id);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to deactivate product {ProductId}. Status code: {StatusCode}", id, response.StatusCode);

                TempData["ProductDeleteError"] = "The product could not be deactivated. Please try again.";

                return RedirectToList(NormalizeFilter(show), id);
            }

            TempData["ProductDeactivated"] = "Product deactivated. You can reactivate it from Edit.";

            // An active product disappears from the Active view when deactivated.
            var successFilter = show == "active" ? "inactive" : NormalizeFilter(show);
            return RedirectToList(successFilter, id);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The product API could not be reached while deactivating product {ProductId}", id);

            TempData["ProductDeleteError"] = "The product API could not be reached. Please try again later.";

            return RedirectToList(NormalizeFilter(show), id);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The product API request timed out while deactivating product {ProductId}", id);

            TempData["ProductDeleteError"] = "The product API request timed out. Please try again later.";

            return RedirectToList(NormalizeFilter(show), id);
        }
    }

    private IActionResult RedirectToList(string? show, int? expandedId)
    {
        return RedirectToPage("/Officer/Products/Index", new { show, expandedId });
    }

    private static string? NormalizeFilter(string? show)
    {
        return show is "active" or "inactive" ? show : null;
    }
}
