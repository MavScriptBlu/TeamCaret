
/// <summary>
/// Provides methods for interacting with the product endpoints in the CCOS API.
/// </summary>
using System.Net;
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

    /// <summary>
    /// All merchandise, including inactive products, from GET /api/products?includeInactive=true.
    /// For the officer product list.
    /// </summary>
    public async Task<IReadOnlyList<ProductDto>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await http.GetFromJsonAsync<List<ProductDto>>("api/products?includeInactive=true", cancellationToken) ?? [];
    }

    /// <summary>
    /// One active product from GET /api/products/{id}, or null when there's no active product with that id.
    /// </summary>
    public async Task<ProductDto?> GetAsync(int productId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/products/{productId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDto>(cancellationToken);
    }

    /// <summary>
    /// sends a new product to POST /api/products, caller checks and disposes the returned response
    /// </summary>
    public Task<HttpResponseMessage> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        return http.PostAsJsonAsync("api/products", request, cancellationToken);
    }

    /// <summary>
    /// Finds one product in the officer list
    /// </summary>
    public async Task<ProductDto?> GetForEditAsync(int productId, CancellationToken cancellationToken = default)
    {
        var products = await ListAllAsync(cancellationToken);

        return products.FirstOrDefault(p => p.ProductId == productId);
    }

    /// <summary>
    /// Sends updated product data to PUT /api/products/{id}
    /// </summary>
    public Task<HttpResponseMessage> UpdateAsync(int productId, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        return http.PutAsJsonAsync($"api/products/{productId}", request, cancellationToken);
    }
}
