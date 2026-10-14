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

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ProductDto> all;
        try
        {
            all = await productsApi.ListAllAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't load products from CCOS.Api.");
            LoadFailed = true;
            return;
        }

        TotalCount = all.Count;
        Products = Show switch
        {
            "active" => all.Where(product => product.IsActive).ToList(),
            "inactive" => all.Where(product => !product.IsActive).ToList(),
            _ => all,
        };
    }
}
