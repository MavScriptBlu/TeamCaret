using CCOS.Shared;

namespace CCOS.Web.Services;

/// <summary>
/// Calls the product endpoints in CCOS.Api (view → API client → Web API → repository → data).
/// The API address comes from Api:BaseUrl in appsettings.json.
/// </summary>
public sealed class ProductsApiClient(HttpClient http)
{
    /// <summary>
    /// Active merchandise from GET /api/products.
    /// </summary>
    public async Task<IReadOnlyList<ProductDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<List<ProductDto>>("api/products", cancellationToken) ?? [];
    }
}
