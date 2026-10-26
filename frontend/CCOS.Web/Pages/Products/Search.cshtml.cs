using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Products;

/// <summary>
/// Product search page (/Products/Search?q=hoodie). The search box in the site header sends
/// visitors here. Shows active products whose name, description or category contains the
/// search term. When nothing matches, shows "Search for X returned no results" with two
/// suggested products (CAR-36, Wireframe-EmptyState in assets/Wireframes-Product).
/// </summary>
public class SearchModel(ProductsApiClient productsApi, ILogger<SearchModel> logger) : PageModel
{
    /// <summary>How many suggested products the "no results" message shows.</summary>
    private const int SuggestionCount = 2;

    /// <summary>
    /// The search term from the header search box (/Products/Search?q=...).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    /// <summary>
    /// Products that match the search term.
    /// </summary>
    public IReadOnlyList<ProductDto> Results { get; private set; } = [];

    /// <summary>
    /// Products to suggest when the search finds nothing. In-stock products come first.
    /// </summary>
    public IReadOnlyList<ProductDto> Suggestions { get; private set; } = [];

    /// <summary>
    /// True when the API couldn't be reached, so the page shows an error message instead.
    /// </summary>
    public bool LoadFailed { get; private set; }

    /// <summary>
    /// True when the visitor typed something to search for.
    /// </summary>
    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);

    /// <summary>
    /// The search term without extra spaces, for matching and for showing on the page.
    /// </summary>
    public string SearchTerm => Query?.Trim() ?? string.Empty;

    /// <summary>
    /// Loads every active product and keeps the ones that match the search term. When nothing
    /// matches, picks a few products to suggest instead. If the API can't be reached,
    /// LoadFailed is set so the page shows an error message.
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // Nothing typed (for example, Enter on an empty search box): the page asks for a search term.
        if (!HasQuery)
        {
            return;
        }

        IReadOnlyList<ProductDto> all;
        try
        {
            all = await productsApi.ListAsync(cancellationToken);
        }
        // Same handling as the browse page: a timeout counts as a failure, but a visitor
        // closing the page (cancellation) is not an error worth logging.
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't load products from CCOS.Api for search.");
            LoadFailed = true;
            return;
        }

        Results = all.Where(product => Matches(product, SearchTerm)).ToList();

        // No matches: suggest products the visitor can actually buy, in-stock ones first.
        if (Results.Count == 0)
        {
            Suggestions = all
                .OrderByDescending(product => product.StockQuantity > 0)
                .Take(SuggestionCount)
                .ToList();
        }
    }

    /// <summary>
    /// True when the product's name, category or description contains the term (any capitalization).
    /// </summary>
    private static bool Matches(ProductDto product, string term) =>
        product.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
        || product.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
        || (product.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
}
